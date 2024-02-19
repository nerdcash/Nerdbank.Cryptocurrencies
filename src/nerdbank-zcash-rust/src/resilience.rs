use std::time::Duration;

use futures_util::Future;
use tokio::select;
use tokio_util::sync::CancellationToken;
use tonic::Status;
use tracing::warn;

const ATTEMPT_LIMIT: u32 = 3;
const DELAY_BETWEEN_RETRIES: Duration = Duration::from_secs(2);

pub(crate) async fn webrequest_with_logged_retry<
    TResult,
    FRequest: FnMut() -> FResult,
    FResult: Future<Output = Result<TResult, Status>>,
    FRetry: Fn(Status, Duration, u32),
>(
    mut delegate: FRequest,
    on_retry: FRetry,
    cancellation_token: CancellationToken,
) -> Result<TResult, Status> {
    let mut failure_count = 0;
    loop {
        let result = select! {
            r = delegate() => r,
            _ = cancellation_token.cancelled() => {
                return Err(Status::cancelled("Request cancelled"));
            },
        };
        match result {
            Ok(result) => return Ok(result),
            Err(status) => {
                if failure_count == ATTEMPT_LIMIT {
                    warn!("Web request failed. No more retries. {:?}", status);
                    return Err(status);
                } else {
                    failure_count += 1;
                    on_retry(status, DELAY_BETWEEN_RETRIES, failure_count);
                    tokio::time::sleep(DELAY_BETWEEN_RETRIES).await;
                }
            }
        }
    }
}

pub(crate) async fn webrequest_with_retry<
    TResult,
    FRequest: FnMut() -> FResult,
    FResult: Future<Output = Result<TResult, Status>>,
>(
    mut delegate: FRequest,
    cancellation_token: CancellationToken,
) -> Result<TResult, Status> {
    let mut failure_count = 0;
    loop {
        let result = select! {
            r = delegate() => r,
            _ = cancellation_token.cancelled() => {
                return Err(Status::cancelled("Request cancelled"));
            },
        };
        match result {
            Ok(result) => return Ok(result),
            Err(status) => {
                if status.code() == tonic::Code::Cancelled {
                    return Err(Status::cancelled(status.message()));
                }
                if failure_count == ATTEMPT_LIMIT {
                    warn!("Web request failed. No more retries. {:?}", status);
                    return Err(status);
                } else {
                    failure_count += 1;
                    select! {
                        _ = tokio::time::sleep(DELAY_BETWEEN_RETRIES) => {},
                        _ = cancellation_token.cancelled() => {
                            return Err(Status::cancelled("Request cancelled"));
                        },
                    }
                }
            }
        }
    }
}
