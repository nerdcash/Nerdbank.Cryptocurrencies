//! Low-overhead timing for one wallet sync.
//!
//! Collection is off unless `NERDBANK_ZCASH_SYNC_TIMING` is `1`, `true`, `on`, or `yes`.
//! When it is off, phase helpers do not read the clock. When it is on, a summary is written to
//! stderr and emitted as a `tracing` info event at the end of `sync`, and after each pass in
//! continual mode.
//!
//! `scan_cached_blocks` both scans and then persists note commitment trees through
//! `WalletWrite::put_blocks`. Upstream marks that method with a `tracing` span named
//! `put_blocks`. While a timed scan runs, a thread-local subscriber records that span so scan
//! CPU and the database write can be reported separately. The subscriber does not enable any
//! other span. The callsite cache is rebuilt after that subscriber is gone, including when the
//! scan unwinds, so its interests and max-level hint do not stay enabled.
//!
//! Each summary lists only the scan ranges finished since the previous summary. Phase totals
//! and the block count stay cumulative for the whole sync.

use std::{
    collections::HashMap,
    fmt::Write as _,
    sync::{
        Arc, Mutex,
        atomic::{AtomicU64, Ordering},
    },
    time::{Duration, Instant},
};

use tracing::{
    Event, Metadata, Subscriber,
    span::{Attributes, Id, Record},
};

/// Environment variable that turns sync timing on.
pub(crate) const ENV_VAR: &str = "NERDBANK_ZCASH_SYNC_TIMING";

const PHASE_COUNT: usize = 7;

tokio::task_local! {
    /// Timing state for the task currently inside [`crate::sync::sync`].
    ///
    /// Spawned download and scan tasks do not inherit this. They use the
    /// [`SyncTiming`] value stored on `SyncState`.
    pub(crate) static SYNC_TIMING: SyncTiming;
}

/// A phase of wallet sync whose busy time is reported in the summary.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum Phase {
    /// Compact-block streaming from lightwalletd (`GetBlockRange`).
    BlockDownload,
    /// `GetTreeState` and `GetSubtreeRoots` round trips.
    TreeState,
    /// `scan_cached_blocks`, excluding time inside the `put_blocks` span.
    Scan,
    /// `put_blocks` (note commitment trees) and subtree-root persistence.
    DbWrite,
    /// Transparent address `GetTaddressTxids` round trips.
    ///
    /// Overlapping calls each add their own busy time, so this phase can exceed wall time.
    /// Local apply stays on the sync task, in address order, and is not included.
    TransparentTxids,
    /// Full transaction `GetTransaction` round trips.
    ///
    /// Overlapping calls each add their own busy time. Decrypt-and-store stays on the sync
    /// task, in txid order, and is not included.
    TxEnhance,
    /// Wallet database open and load (`WalletDb::for_path`, including init when requested).
    DbOpen,
}

impl Phase {
    const ALL: [Phase; PHASE_COUNT] = [
        Phase::BlockDownload,
        Phase::TreeState,
        Phase::Scan,
        Phase::DbWrite,
        Phase::TransparentTxids,
        Phase::TxEnhance,
        Phase::DbOpen,
    ];

    fn index(self) -> usize {
        match self {
            Phase::BlockDownload => 0,
            Phase::TreeState => 1,
            Phase::Scan => 2,
            Phase::DbWrite => 3,
            Phase::TransparentTxids => 4,
            Phase::TxEnhance => 5,
            Phase::DbOpen => 6,
        }
    }

    fn label(self) -> &'static str {
        match self {
            Phase::BlockDownload => "block_download",
            Phase::TreeState => "tree_state",
            Phase::Scan => "scan",
            Phase::DbWrite => "db_write",
            Phase::TransparentTxids => "transparent_txids",
            Phase::TxEnhance => "tx_enhance",
            Phase::DbOpen => "db_open",
        }
    }
}

#[derive(Clone, Copy, Debug, Default)]
struct PhaseStats {
    busy: Duration,
    calls: u64,
}

struct OpenRange {
    start_height: u32,
    end_height: u32,
    priority: String,
    started: Instant,
    blocks: u64,
    phases: [PhaseStats; PHASE_COUNT],
}

#[derive(Clone, Debug)]
struct FinishedRange {
    start_height: u32,
    end_height: u32,
    priority: String,
    wall: Duration,
    blocks: u64,
    phases: [PhaseStats; PHASE_COUNT],
}

struct State {
    started: Instant,
    idle: Duration,
    blocks: u64,
    put_blocks_observations: u64,
    phases: [PhaseStats; PHASE_COUNT],
    current_range: Option<OpenRange>,
    ranges: Vec<FinishedRange>,
}

/// Shared sync timing state.
///
/// A disabled value (`inner` is `None`) ignores every record. Clones share the same counters.
#[derive(Clone)]
pub(crate) struct SyncTiming {
    inner: Option<Arc<Mutex<State>>>,
}

impl std::fmt::Debug for SyncTiming {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("SyncTiming")
            .field("enabled", &self.is_enabled())
            .finish()
    }
}

impl SyncTiming {
    /// Timing state for this task, or a disabled value when sync timing is not active.
    ///
    /// Spawned tasks must use the [`SyncTiming`] copied onto `SyncState` instead.
    pub(crate) fn current() -> Self {
        SYNC_TIMING
            .try_with(|timing| timing.clone())
            .unwrap_or_else(|_| Self::disabled())
    }

    /// Builds a timing handle from [`ENV_VAR`].
    pub(crate) fn from_env() -> Self {
        match std::env::var(ENV_VAR) {
            Ok(value) if flag_enabled(Some(&value)) => Self::enabled(),
            _ => Self::disabled(),
        }
    }

    pub(crate) fn enabled() -> Self {
        Self {
            inner: Some(Arc::new(Mutex::new(State {
                started: Instant::now(),
                idle: Duration::ZERO,
                blocks: 0,
                put_blocks_observations: 0,
                phases: [PhaseStats::default(); PHASE_COUNT],
                current_range: None,
                ranges: Vec::new(),
            }))),
        }
    }

    pub(crate) fn disabled() -> Self {
        Self { inner: None }
    }

    pub(crate) fn is_enabled(&self) -> bool {
        self.inner.is_some()
    }

    /// Emits the summary when dropped. Cheap when timing is disabled.
    pub(crate) fn finish_guard(&self) -> FinishGuard {
        FinishGuard {
            timing: self.clone(),
        }
    }

    /// Starts a phase timer. The phase is recorded when the guard drops.
    pub(crate) fn start(&self, phase: Phase) -> PhaseGuard {
        match &self.inner {
            Some(_) => PhaseGuard {
                timing: self.clone(),
                phase,
                start: Some(Instant::now()),
                accumulated: Duration::ZERO,
                active: true,
            },
            None => PhaseGuard::inactive(),
        }
    }

    /// Timer for [`crate::backing_store`] database open/load on the sync task.
    pub(crate) fn db_open_timer() -> PhaseGuard {
        SYNC_TIMING
            .try_with(|timing| timing.start(Phase::DbOpen))
            .unwrap_or_else(|_| PhaseGuard::inactive())
    }

    /// Opens a scan-range bucket. Drop the guard to close it.
    ///
    /// Heights are inclusive-exclusive, matching `ScanRange`.
    pub(crate) fn begin_range(&self, start: u32, end: u32, priority: &str) -> RangeGuard {
        if let Some(inner) = &self.inner {
            let mut state = lock(inner);
            if state.current_range.is_some() {
                close_range(&mut state);
            }
            state.current_range = Some(OpenRange {
                start_height: start,
                end_height: end,
                priority: priority.to_owned(),
                started: Instant::now(),
                blocks: 0,
                phases: [PhaseStats::default(); PHASE_COUNT],
            });
        }
        RangeGuard {
            timing: self.clone(),
        }
    }

    /// Omits `busy` from the wall time used for blocks/sec.
    ///
    /// Used around the mempool watch so continual sync does not count idle time as work.
    pub(crate) fn begin_idle(&self) -> IdleGuard {
        IdleGuard {
            timing: self.clone(),
            start: self.inner.as_ref().map(|_| Instant::now()),
        }
    }

    pub(crate) fn add_blocks(&self, count: u64) {
        let Some(inner) = &self.inner else {
            return;
        };
        let mut state = lock(inner);
        state.blocks = state.blocks.saturating_add(count);
        if let Some(range) = state.current_range.as_mut() {
            range.blocks = range.blocks.saturating_add(count);
        }
    }

    /// Runs `scan` and splits `put_blocks` span time out of the surrounding scan time.
    ///
    /// When timing is disabled this calls `scan` directly.
    pub(crate) fn measure_scan<T>(&self, scan: impl FnOnce() -> T) -> T {
        if self.inner.is_none() {
            return scan();
        }

        let tracker = Arc::new(Mutex::new(OutermostSpan::default()));
        let subscriber = PutBlocksSubscriber {
            next_id: AtomicU64::new(1),
            ids: Mutex::new(HashMap::new()),
            tracker: Arc::clone(&tracker),
        };
        // Drop the restore guard only after `with_default` has removed the temporary
        // dispatcher. Rebuilding inside the closure records this subscriber's callsite
        // interests and max-level hint, and those stay in effect once it is gone.
        // The guard also runs when `scan` unwinds: `with_default` restores the previous
        // dispatcher first, then this drop rebuilds against that dispatcher.
        let _restore = RestoreInterestCache;
        let (result, total) = tracing::subscriber::with_default(subscriber, || {
            tracing::callsite::rebuild_interest_cache();
            let started = Instant::now();
            let result = scan();
            (result, started.elapsed())
        });
        drop(_restore);

        let (busy, saw) = {
            let observed = lock(&tracker);
            (observed.busy, observed.saw)
        };
        self.record_duration(Phase::Scan, total.saturating_sub(busy));
        if saw {
            self.record_duration(Phase::DbWrite, busy);
            self.note_put_blocks();
        }
        result
    }

    /// Prints one summary, then drops scan-range rows already included in it.
    ///
    /// Phase totals and the block count are kept. The next summary therefore stays
    /// cumulative without reprinting every earlier range. In continual mode that keeps
    /// each pass from emitting the full history again.
    pub(crate) fn emit(&self, label: &str) {
        let Some(summary) = self.take_summary(label) else {
            return;
        };
        tracing::info!("{summary}");
        eprintln!("{summary}");
    }

    /// Formats the current summary and clears reported scan ranges.
    fn take_summary(&self, label: &str) -> Option<String> {
        let inner = self.inner.as_ref()?;
        let mut state = lock(inner);
        let summary = format_summary(label, &state);
        state.ranges.clear();
        Some(summary)
    }

    fn end_range(&self) {
        let Some(inner) = &self.inner else {
            return;
        };
        let mut state = lock(inner);
        close_range(&mut state);
    }

    fn add_idle(&self, idle: Duration) {
        let Some(inner) = &self.inner else {
            return;
        };
        let mut state = lock(inner);
        state.idle = state.idle.saturating_add(idle);
    }

    fn record_duration(&self, phase: Phase, busy: Duration) {
        let Some(inner) = &self.inner else {
            return;
        };
        let mut state = lock(inner);
        add_phase(&mut state.phases[phase.index()], busy);
        if let Some(range) = state.current_range.as_mut() {
            add_phase(&mut range.phases[phase.index()], busy);
        }
    }

    fn note_put_blocks(&self) {
        let Some(inner) = &self.inner else {
            return;
        };
        let mut state = lock(inner);
        state.put_blocks_observations = state.put_blocks_observations.saturating_add(1);
    }
}

fn add_phase(stats: &mut PhaseStats, busy: Duration) {
    stats.busy = stats.busy.saturating_add(busy);
    stats.calls = stats.calls.saturating_add(1);
}

fn close_range(state: &mut State) {
    if let Some(open) = state.current_range.take() {
        state.ranges.push(FinishedRange {
            start_height: open.start_height,
            end_height: open.end_height,
            priority: open.priority,
            wall: open.started.elapsed(),
            blocks: open.blocks,
            phases: open.phases,
        });
    }
}

fn lock<T>(mutex: &Mutex<T>) -> std::sync::MutexGuard<'_, T> {
    mutex.lock().unwrap_or_else(|err| err.into_inner())
}

fn flag_enabled(value: Option<&str>) -> bool {
    matches!(
        value
            .map(|text| text.trim().to_ascii_lowercase())
            .as_deref(),
        Some("1") | Some("true") | Some("on") | Some("yes")
    )
}

/// Records one phase when dropped.
#[must_use = "phase time is recorded when the guard drops"]
pub(crate) struct PhaseGuard {
    timing: SyncTiming,
    phase: Phase,
    start: Option<Instant>,
    accumulated: Duration,
    active: bool,
}

impl PhaseGuard {
    fn inactive() -> Self {
        Self {
            timing: SyncTiming::disabled(),
            phase: Phase::DbOpen,
            start: None,
            accumulated: Duration::ZERO,
            active: false,
        }
    }

    /// Stops the clock without recording the phase yet.
    pub(crate) fn pause(&mut self) {
        if let Some(start) = self.start.take() {
            self.accumulated = self.accumulated.saturating_add(start.elapsed());
        }
    }

    /// Resumes the clock after [`Self::pause`].
    pub(crate) fn resume(&mut self) {
        if self.active && self.start.is_none() {
            self.start = Some(Instant::now());
        }
    }
}

impl Drop for PhaseGuard {
    fn drop(&mut self) {
        if !self.active {
            return;
        }
        if let Some(start) = self.start.take() {
            self.accumulated = self.accumulated.saturating_add(start.elapsed());
        }
        self.timing.record_duration(self.phase, self.accumulated);
    }
}

/// Closes the current scan-range bucket when dropped.
#[must_use = "the scan range stays open until the guard drops"]
pub(crate) struct RangeGuard {
    timing: SyncTiming,
}

impl Drop for RangeGuard {
    fn drop(&mut self) {
        self.timing.end_range();
    }
}

/// Excludes the guarded interval from wall time used for blocks/sec.
#[must_use = "idle time is recorded when the guard drops"]
pub(crate) struct IdleGuard {
    timing: SyncTiming,
    start: Option<Instant>,
}

impl Drop for IdleGuard {
    fn drop(&mut self) {
        if let Some(start) = self.start.take() {
            self.timing.add_idle(start.elapsed());
        }
    }
}

/// Emits the sync summary when dropped.
#[must_use = "the summary is emitted when the guard drops"]
pub(crate) struct FinishGuard {
    timing: SyncTiming,
}

impl Drop for FinishGuard {
    fn drop(&mut self) {
        self.timing.emit("sync finished");
    }
}

/// Accumulates only the outermost enter/exit pair.
///
/// `put_blocks` on `WalletDb` calls another `put_blocks` on the SQL transaction. Only the outer
/// method is instrumented today; counting the outermost span stays correct if both are.
#[derive(Debug, Default)]
struct OutermostSpan {
    depth: u32,
    started: Option<Instant>,
    busy: Duration,
    saw: bool,
}

impl OutermostSpan {
    fn enter(&mut self, now: Instant) {
        if self.depth == 0 {
            self.started = Some(now);
            self.saw = true;
        }
        self.depth += 1;
    }

    fn exit(&mut self, now: Instant) {
        if self.depth == 0 {
            return;
        }
        self.depth -= 1;
        if self.depth == 0
            && let Some(started) = self.started.take()
        {
            self.busy = self
                .busy
                .saturating_add(now.saturating_duration_since(started));
        }
    }
}

/// Rebuilds tracing callsite interest when dropped.
///
/// Construct this before [`tracing::subscriber::with_default`] and drop it after that
/// call returns. `with_default` removes its dispatcher before unwinding out of the
/// closure, so this drop then rebuilds against the restored dispatcher.
struct RestoreInterestCache;

impl Drop for RestoreInterestCache {
    fn drop(&mut self) {
        tracing::callsite::rebuild_interest_cache();
    }
}

struct PutBlocksSubscriber {
    next_id: AtomicU64,
    ids: Mutex<HashMap<u64, bool>>,
    tracker: Arc<Mutex<OutermostSpan>>,
}

impl PutBlocksSubscriber {
    fn is_put_blocks(&self, span: &Id) -> bool {
        lock(&self.ids)
            .get(&span.into_u64())
            .copied()
            .unwrap_or(false)
    }
}

impl Subscriber for PutBlocksSubscriber {
    fn enabled(&self, metadata: &Metadata<'_>) -> bool {
        metadata.is_span() && metadata.name() == "put_blocks"
    }

    fn new_span(&self, span: &Attributes<'_>) -> Id {
        let id = self.next_id.fetch_add(1, Ordering::Relaxed).max(1);
        let is_put_blocks = span.metadata().name() == "put_blocks";
        lock(&self.ids).insert(id, is_put_blocks);
        Id::from_u64(id)
    }

    fn record(&self, _span: &Id, _values: &Record<'_>) {}

    fn record_follows_from(&self, _span: &Id, _follows: &Id) {}

    fn event(&self, _event: &Event<'_>) {}

    fn enter(&self, span: &Id) {
        if self.is_put_blocks(span) {
            lock(&self.tracker).enter(Instant::now());
        }
    }

    fn exit(&self, span: &Id) {
        if self.is_put_blocks(span) {
            lock(&self.tracker).exit(Instant::now());
        }
    }
}

fn format_summary(label: &str, state: &State) -> String {
    let wall = state.started.elapsed().saturating_sub(state.idle);
    let missing_put_blocks = state.blocks > 0
        && state.put_blocks_observations == 0
        && state.phases[Phase::Scan.index()].calls > 0;
    format_summary_parts(
        label,
        wall,
        state.blocks,
        &state.phases,
        &state.ranges,
        missing_put_blocks,
    )
}

fn format_summary_parts(
    label: &str,
    wall: Duration,
    blocks: u64,
    phases: &[PhaseStats; PHASE_COUNT],
    ranges: &[FinishedRange],
    missing_put_blocks: bool,
) -> String {
    let mut out = String::new();
    let _ = writeln!(
        out,
        "zcash sync timing ({label}): wall {}, blocks {blocks}, {} blocks/sec",
        format_secs(wall),
        format_rate(blocks, wall),
    );
    let _ = writeln!(
        out,
        "{:<18} {:>10} {:>8} {:>8} {:>8}",
        "phase", "busy", "wall%", "busy%", "calls"
    );
    let busy_total = phases.iter().fold(Duration::ZERO, |total, phase| {
        total.saturating_add(phase.busy)
    });
    for phase in Phase::ALL {
        let stats = phases[phase.index()];
        let _ = writeln!(
            out,
            "{:<18} {:>10} {:>8} {:>8} {:>8}",
            phase.label(),
            format_secs(stats.busy),
            format_pct(share(stats.busy, wall)),
            format_pct(share(stats.busy, busy_total)),
            stats.calls,
        );
    }
    let _ = writeln!(out, "scan ranges (included in the totals above):");
    if ranges.is_empty() {
        let _ = writeln!(out, "  (none)");
    }
    for range in ranges {
        let _ = write!(
            out,
            "  [{},{}) {} blocks={} wall={} {} blocks/sec",
            range.start_height,
            range.end_height,
            range.priority,
            range.blocks,
            format_secs(range.wall),
            format_rate(range.blocks, range.wall),
        );
        for phase in Phase::ALL {
            let _ = write!(
                out,
                " {}={}",
                phase.label(),
                format_secs(range.phases[phase.index()].busy),
            );
        }
        let _ = writeln!(out);
    }
    let _ = writeln!(
        out,
        "legend: block_download=lightwalletd compact-block download; \
         tree_state=lightwalletd tree-state and subtree-root fetches; \
         scan=scan_cached_blocks excluding put_blocks; \
         db_write=put_blocks commitment-tree persistence and subtree-root writes; \
         transparent_txids=GetTaddressTxids round trips (concurrent calls sum busy time); \
         tx_enhance=GetTransaction round trips (concurrent calls sum busy time); \
         db_open=wallet database open/load. \
         Local transparent apply and decrypt-and-store stay ordered on the sync task and are not included."
    );
    let _ = writeln!(
        out,
        "note: download and scanning overlap, and concurrent calls in one phase sum busy time, \
         so wall% and a single phase can pass 100. \
         busy% is each phase's share of measured busy time. \
         wall is elapsed sync time excluding waits for the next block in continual mode. \
         Initial server connection, chain-tip polls, and UI reporting are in wall but not in a phase. \
         Scan ranges are since the previous summary; phase totals and the block count are cumulative."
    );
    if missing_put_blocks {
        let _ = writeln!(
            out,
            "warning: scanned blocks but never observed the put_blocks span; \
             scan time includes commitment-tree persistence."
        );
    }
    out
}

fn share(part: Duration, whole: Duration) -> f64 {
    let whole_secs = whole.as_secs_f64();
    if whole_secs == 0.0 {
        0.0
    } else {
        part.as_secs_f64() / whole_secs * 100.0
    }
}

fn format_secs(duration: Duration) -> String {
    format!("{:.3}s", duration.as_secs_f64())
}

fn format_rate(blocks: u64, wall: Duration) -> String {
    let wall_secs = wall.as_secs_f64();
    let rate = if wall_secs == 0.0 {
        0.0
    } else {
        blocks as f64 / wall_secs
    };
    format!("{rate:.2}")
}

fn format_pct(pct: f64) -> String {
    format!("{pct:.1}%")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn flag_parses_common_spellings() {
        assert!(!flag_enabled(None));
        assert!(!flag_enabled(Some("")));
        assert!(!flag_enabled(Some("0")));
        assert!(!flag_enabled(Some("false")));
        assert!(!flag_enabled(Some("off")));
        assert!(!flag_enabled(Some("no")));
        assert!(!flag_enabled(Some("maybe")));
        assert!(flag_enabled(Some("1")));
        assert!(flag_enabled(Some(" true ")));
        assert!(flag_enabled(Some("TRUE")));
        assert!(flag_enabled(Some("on")));
        assert!(flag_enabled(Some("yes")));
    }

    #[test]
    fn phase_indexes_cover_the_summary_table() {
        assert_eq!(Phase::ALL.len(), PHASE_COUNT);
        for (index, phase) in Phase::ALL.iter().copied().enumerate() {
            assert_eq!(phase.index(), index);
        }
    }

    #[test]
    fn disabled_timing_does_not_record() {
        let timing = SyncTiming::disabled();
        timing.record_duration(Phase::Scan, Duration::from_secs(5));
        timing.add_blocks(10);
        let _range = timing.begin_range(1, 2, "Historic");
        assert_eq!(timing.phase_calls(Phase::Scan), 0);
        assert_eq!(timing.finished_range_count(), 0);
        assert!(!timing.is_enabled());
    }

    #[test]
    fn records_phases_blocks_and_ranges() {
        let timing = SyncTiming::enabled();
        {
            let _range = timing.begin_range(100, 200, "Historic");
            timing.record_duration(Phase::Scan, Duration::from_millis(400));
            timing.record_duration(Phase::BlockDownload, Duration::from_millis(250));
            timing.add_blocks(40);
        }
        timing.record_duration(Phase::DbOpen, Duration::from_millis(15));
        assert_eq!(timing.phase_calls(Phase::Scan), 1);
        assert_eq!(timing.phase_busy(Phase::Scan), Duration::from_millis(400));
        assert_eq!(timing.phase_calls(Phase::DbOpen), 1);
        assert_eq!(timing.finished_range_count(), 1);
        assert_eq!(timing.range_blocks(0), 40);
        assert_eq!(
            timing.range_phase_busy(0, Phase::BlockDownload),
            Duration::from_millis(250)
        );
        assert_eq!(timing.range_phase_busy(0, Phase::DbOpen), Duration::ZERO);
        assert_eq!(timing.block_count(), 40);
    }

    #[test]
    fn phase_guard_records_once_across_pause() {
        let timing = SyncTiming::enabled();
        {
            let mut guard = timing.start(Phase::TreeState);
            guard.pause();
            guard.resume();
        }
        assert_eq!(timing.phase_calls(Phase::TreeState), 1);
    }

    #[test]
    fn db_open_timer_outside_a_sync_task_is_inert() {
        let _timer = SyncTiming::db_open_timer();
    }

    #[tokio::test]
    async fn db_open_timer_records_on_the_sync_task() {
        let timing = SyncTiming::enabled();
        SYNC_TIMING
            .scope(timing.clone(), async {
                let _timer = SyncTiming::db_open_timer();
            })
            .await;
        assert_eq!(timing.phase_calls(Phase::DbOpen), 1);
    }

    #[test]
    fn idle_guard_is_subtracted_from_reported_wall() {
        let timing = SyncTiming::enabled();
        {
            let _idle = timing.begin_idle();
            std::thread::sleep(Duration::from_millis(20));
        }
        let state = lock(timing.inner.as_ref().expect("enabled"));
        assert!(state.idle >= Duration::from_millis(15));
        let summary = format_summary("sync pass", &state);
        drop(state);
        assert!(summary.contains("sync pass"));
    }

    #[test]
    fn outermost_span_does_not_double_count_nested_enters() {
        let start = Instant::now();
        let mut span = OutermostSpan::default();
        span.enter(start);
        span.enter(start + Duration::from_millis(10));
        span.exit(start + Duration::from_millis(25));
        span.exit(start + Duration::from_millis(40));
        span.enter(start + Duration::from_millis(50));
        span.exit(start + Duration::from_millis(70));
        span.exit(start + Duration::from_millis(90));
        assert!(span.saw);
        assert_eq!(span.depth, 0);
        assert_eq!(span.busy, Duration::from_millis(60));
    }

    #[test]
    fn summary_reports_rates_shares_and_ranges() {
        let mut phases = [PhaseStats::default(); PHASE_COUNT];
        phases[Phase::BlockDownload.index()] = PhaseStats {
            busy: Duration::from_secs(4),
            calls: 2,
        };
        phases[Phase::Scan.index()] = PhaseStats {
            busy: Duration::from_secs(6),
            calls: 2,
        };
        let ranges = [FinishedRange {
            start_height: 100,
            end_height: 200,
            priority: "Historic".to_owned(),
            wall: Duration::from_secs(2),
            blocks: 50,
            phases,
        }];
        let text = format_summary_parts(
            "sync finished",
            Duration::from_secs(10),
            100,
            &phases,
            &ranges,
            false,
        );
        assert!(text.contains(
            "zcash sync timing (sync finished): wall 10.000s, blocks 100, 10.00 blocks/sec"
        ));
        assert!(text.contains("block_download"));
        assert!(text.contains("4.000s"));
        assert!(text.contains("40.0%"));
        assert!(text.contains("60.0%"));
        assert!(text.contains("[100,200) Historic blocks=50 wall=2.000s 25.00 blocks/sec"));
        assert!(text.contains("transparent_txids"));
        assert!(text.contains("note: download and scanning overlap"));
        assert!(!text.contains("warning:"));

        let warned = format_summary_parts(
            "sync finished",
            Duration::from_secs(10),
            100,
            &phases,
            &[],
            true,
        );
        assert!(warned.contains("scan ranges (included in the totals above):\n  (none)"));
        assert!(warned.contains("never observed the put_blocks span"));
    }

    #[test]
    fn zero_wall_does_not_divide_by_zero() {
        let phases = [PhaseStats::default(); PHASE_COUNT];
        let text = format_summary_parts("sync finished", Duration::ZERO, 0, &phases, &[], false);
        assert!(text.contains("0.00 blocks/sec"));
        assert!(text.contains("0.0%"));
    }

    #[test]
    fn measure_scan_observes_put_blocks_and_ignores_other_spans() {
        let _tracing = lock_tracing_cache();
        let timing = SyncTiming::enabled();
        timing.measure_scan(|| {
            let _outer = tracing::info_span!("put_blocks").entered();
            std::thread::sleep(Duration::from_millis(20));
            let _inner = tracing::info_span!("put_blocks").entered();
            std::thread::sleep(Duration::from_millis(20));
        });
        assert_eq!(timing.phase_calls(Phase::Scan), 1);
        assert_eq!(timing.phase_calls(Phase::DbWrite), 1);
        assert!(timing.phase_busy(Phase::DbWrite) >= Duration::from_millis(35));
        assert!(timing.phase_busy(Phase::Scan) < timing.phase_busy(Phase::DbWrite));

        let timing = SyncTiming::enabled();
        timing.measure_scan(|| {
            let _other = tracing::info_span!("scan_cached_blocks").entered();
            std::thread::sleep(Duration::from_millis(15));
        });
        assert_eq!(timing.phase_calls(Phase::DbWrite), 0);
        assert!(timing.phase_busy(Phase::Scan) >= Duration::from_millis(10));
        assert_eq!(timing.put_blocks_observations(), 0);
    }

    #[test]
    fn disabled_measure_scan_skips_the_subscriber() {
        let timing = SyncTiming::disabled();
        let value = timing.measure_scan(|| 7);
        assert_eq!(value, 7);
        assert_eq!(timing.phase_calls(Phase::Scan), 0);
    }

    #[test]
    fn measure_scan_restores_callsite_interest_after_the_temporary_subscriber() {
        let _tracing = lock_tracing_cache();
        let timing = SyncTiming::enabled();
        timing.measure_scan(|| {
            let _entered = put_blocks_span().entered();
        });
        assert_eq!(timing.phase_calls(Phase::DbWrite), 1);
        assert!(
            put_blocks_span().is_disabled(),
            "put_blocks callsite stayed enabled after the temporary subscriber was removed"
        );
    }

    #[test]
    fn measure_scan_restores_callsite_interest_when_scan_unwinds() {
        let _tracing = lock_tracing_cache();
        let timing = SyncTiming::enabled();
        let caught = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
            timing.measure_scan(|| {
                let _entered = put_blocks_span().entered();
                panic!("scan failed");
            });
        }));
        assert!(caught.is_err());
        assert!(
            put_blocks_span().is_disabled(),
            "put_blocks callsite stayed enabled after a timed scan unwound"
        );
    }

    #[test]
    fn emit_drops_reported_ranges_and_keeps_cumulative_totals() {
        let timing = SyncTiming::enabled();
        {
            let _range = timing.begin_range(100, 110, "Historic");
            timing.record_duration(Phase::BlockDownload, Duration::from_millis(4));
            timing.add_blocks(10);
        }
        let first = timing.take_summary("sync pass").expect("timing is enabled");
        assert!(first.contains("[100,110) Historic"));
        assert!(first.contains("blocks 10"));
        assert!(first.contains("phase totals and the block count are cumulative"));
        assert_eq!(timing.finished_range_count(), 0);
        assert_eq!(timing.phase_calls(Phase::BlockDownload), 1);
        assert_eq!(timing.block_count(), 10);

        {
            let _range = timing.begin_range(110, 130, "ChainTip");
            timing.record_duration(Phase::Scan, Duration::from_millis(8));
            timing.add_blocks(20);
        }
        let second = timing.take_summary("sync pass").expect("timing is enabled");
        assert!(second.contains("[110,130) ChainTip"));
        assert!(!second.contains("[100,110)"));
        assert!(second.contains("blocks 30"));
        assert_eq!(timing.phase_calls(Phase::BlockDownload), 1);
        assert_eq!(timing.phase_calls(Phase::Scan), 1);
        assert_eq!(timing.block_count(), 30);
        assert_eq!(timing.finished_range_count(), 0);

        timing.emit("sync pass");
        assert_eq!(timing.finished_range_count(), 0);
        assert_eq!(timing.block_count(), 30);
    }

    /// One callsite for `put_blocks`, shared by the interest-cache tests.
    fn put_blocks_span() -> tracing::Span {
        tracing::info_span!("put_blocks")
    }

    fn lock_tracing_cache() -> std::sync::MutexGuard<'static, ()> {
        static LOCK: std::sync::Mutex<()> = std::sync::Mutex::new(());
        LOCK.lock().unwrap_or_else(|err| err.into_inner())
    }

    impl SyncTiming {
        fn phase_calls(&self, phase: Phase) -> u64 {
            self.inner
                .as_ref()
                .map(|inner| lock(inner).phases[phase.index()].calls)
                .unwrap_or(0)
        }

        fn phase_busy(&self, phase: Phase) -> Duration {
            self.inner
                .as_ref()
                .map(|inner| lock(inner).phases[phase.index()].busy)
                .unwrap_or_default()
        }

        fn block_count(&self) -> u64 {
            self.inner
                .as_ref()
                .map(|inner| lock(inner).blocks)
                .unwrap_or(0)
        }

        fn finished_range_count(&self) -> usize {
            self.inner
                .as_ref()
                .map(|inner| lock(inner).ranges.len())
                .unwrap_or(0)
        }

        fn range_blocks(&self, index: usize) -> u64 {
            lock(self.inner.as_ref().expect("enabled")).ranges[index].blocks
        }

        fn range_phase_busy(&self, index: usize, phase: Phase) -> Duration {
            lock(self.inner.as_ref().expect("enabled")).ranges[index].phases[phase.index()].busy
        }

        fn put_blocks_observations(&self) -> u64 {
            self.inner
                .as_ref()
                .map(|inner| lock(inner).put_blocks_observations)
                .unwrap_or(0)
        }
    }
}
