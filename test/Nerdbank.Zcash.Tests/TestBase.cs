// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public abstract class TestBase
{
    protected const string ValidUnifiedAddressOrchardSaplingTransparent = "u1vv2ws6xhs72faugmlrasyeq298l05rrj6wfw8hr3r29y3czev5qt4ugp7kylz6suu04363ze92dfg8ftxf3237js0x9p5r82fgy47xkjnw75tqaevhfh0rnua72hurt22v3w3f7h8yt6mxaa0wpeeh9jcm359ww3rl6fj5ylqqv54uuwrs8q4gys9r3cxdm3yslsh3rt6p7wznzhky7";
    protected const string ValidUnifiedAddressOrchardSapling = "u10p78pgwpatn9n5zsut79577c78yt59cerl0ymdk7m4ug3hd7cw0fj2c7k20q3ndt2x49zzy69xgl22wr7tgl652lxflaex79xgpg2kyk9m83nzerccpvkxfy47v7xz6g5fqaz3x4tvl6lnkh58j6mj60synt2kr5rgxcpdm3qq9u0nm2";
    protected const string ValidUnifiedAddressOrchard = "u1v0j6szgvcquae449dltsrhdhlle4ac8cxd3z8k4j2wtxgfxg6xnq25a900d3yq65mz0l6heqhcj468f7q3l2wnxdsxjrcw90svum7q67";
    protected const string ValidUnifiedAddressSapling = "u12s5xnr2r6jj4xt72qjx35mru9mq3w3v0mxkvtd67e9fsr4tzf682983qn752kf5fvcdva79pr2udwhg5sm4pw6np90t8q6q8tcu97k6c";
    protected const string ValidSaplingAddress = "zs1znewe2leucm8gsd2ue24kvp3jjjwgrhmytmv0scenaf460kdj70r299a88r8n0pyvwz7c9skfmy";
    protected const string ValidSproutAddress = "zc8E5gYid86n4bo2Usdq1cpr7PpfoJGzttwBHEEgGhGkLUg7SPPVFNB2AkRFXZ7usfphup5426dt1buMmY3fkYeRrQGLa8y"; // I made this one up to just satisfy length and Base58Check encoding.
    protected const string ValidTransparentP2PKHAddress = "t1a7w3qM23i4ajQcbX5wd6oH4zTY8Bry5vF";

    // When we ever get a sample of a valid P2SH address, we can add it here, and then search all code for commented references to this field and enable it.
    ////protected const string ValidTransparentP2SHAddress = "3KQYMMqMBTv8254UqwmaLzW5NDT879KzK8";
}
