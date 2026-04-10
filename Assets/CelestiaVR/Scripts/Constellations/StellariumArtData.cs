namespace CelestiaVR.Constellations
{
    /// <summary>
    /// Art positioning data extracted from Stellarium modern sky culture.
    /// Three HIP-anchor + pixel-coordinate pairs per constellation.
    /// The runtime loader solves the affine map (pixel→sky) to place quads precisely.
    /// Generated automatically — do not hand-edit.
    /// </summary>
    public static class StellariumArtData
    {
        public struct ArtAnchor { public int Hip; public float Px, Py; }

        public struct ConstellationArt
        {
            public string Abbreviation;
            public string PngName;      // filename without extension or path
            public int    ImageW, ImageH;
            public ArtAnchor A1, A2, A3;
        }

        public static readonly ConstellationArt[] All = new ConstellationArt[]
        {
            new ConstellationArt { Abbreviation="Aql", PngName="aquila", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=97649,Px=163f,Py=232f}, A2=new ArtAnchor{Hip=93244,Px=385f,Py=131f}, A3=new ArtAnchor{Hip=93805,Px=397f,Py=397f} },
            new ConstellationArt { Abbreviation="And", PngName="andromeda", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=3881,Px=198f,Py=215f}, A2=new ArtAnchor{Hip=3092,Px=337f,Py=136f}, A3=new ArtAnchor{Hip=9640,Px=224f,Py=428f} },
            new ConstellationArt { Abbreviation="Scl", PngName="sculptor", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=115102,Px=231f,Py=39f}, A2=new ArtAnchor{Hip=116231,Px=240f,Py=106f}, A3=new ArtAnchor{Hip=4577,Px=32f,Py=174f} },
            new ConstellationArt { Abbreviation="Ara", PngName="ara", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=83081,Px=98f,Py=70f}, A2=new ArtAnchor{Hip=85727,Px=191f,Py=93f}, A3=new ArtAnchor{Hip=88714,Px=107f,Py=249f} },
            new ConstellationArt { Abbreviation="Lib", PngName="libra", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=74785,Px=41f,Py=27f}, A2=new ArtAnchor{Hip=77853,Px=58f,Py=170f}, A3=new ArtAnchor{Hip=73714,Px=224f,Py=107f} },
            new ConstellationArt { Abbreviation="Cet", PngName="cetus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=14143,Px=87f,Py=63f}, A2=new ArtAnchor{Hip=12770,Px=28f,Py=274f}, A3=new ArtAnchor{Hip=1562,Px=412f,Py=440f} },
            new ConstellationArt { Abbreviation="Ari", PngName="aries", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=13209,Px=12f,Py=130f}, A2=new ArtAnchor{Hip=13914,Px=58f,Py=206f}, A3=new ArtAnchor{Hip=8832,Px=210f,Py=47f} },
            new ConstellationArt { Abbreviation="Sct", PngName="scutum", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=92175,Px=52f,Py=17f}, A2=new ArtAnchor{Hip=90595,Px=224f,Py=124f}, A3=new ArtAnchor{Hip=92814,Px=139f,Py=206f} },
            new ConstellationArt { Abbreviation="Pyx", PngName="pyxis", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=41723,Px=152f,Py=220f}, A2=new ArtAnchor{Hip=42828,Px=94f,Py=159f}, A3=new ArtAnchor{Hip=42515,Px=44f,Py=202f} },
            new ConstellationArt { Abbreviation="Boo", PngName="bootes", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=72105,Px=225f,Py=222f}, A2=new ArtAnchor{Hip=71075,Px=365f,Py=184f}, A3=new ArtAnchor{Hip=67927,Px=207f,Py=401f} },
            new ConstellationArt { Abbreviation="Cae", PngName="caelum", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=21060,Px=16f,Py=231f}, A2=new ArtAnchor{Hip=21770,Px=76f,Py=145f}, A3=new ArtAnchor{Hip=21861,Px=199f,Py=75f} },
            new ConstellationArt { Abbreviation="Cha", PngName="chamaeleon", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=60000,Px=29f,Py=214f}, A2=new ArtAnchor{Hip=51839,Px=73f,Py=123f}, A3=new ArtAnchor{Hip=40702,Px=184f,Py=33f} },
            new ConstellationArt { Abbreviation="Cnc", PngName="cancer", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=44066,Px=29f,Py=166f}, A2=new ArtAnchor{Hip=40526,Px=101f,Py=255f}, A3=new ArtAnchor{Hip=40843,Px=206f,Py=91f} },
            new ConstellationArt { Abbreviation="Cap", PngName="capricornus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=107556,Px=15f,Py=436f}, A2=new ArtAnchor{Hip=100064,Px=403f,Py=7f}, A3=new ArtAnchor{Hip=102978,Px=460f,Py=438f} },
            new ConstellationArt { Abbreviation="Car", PngName="argonavis", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=45238,Px=202f,Py=455f}, A2=new ArtAnchor{Hip=50191,Px=62f,Py=216f}, A3=new ArtAnchor{Hip=39757,Px=298f,Py=22f} },
            new ConstellationArt { Abbreviation="Cas", PngName="cassiopeia", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=8886,Px=390f,Py=336f}, A2=new ArtAnchor{Hip=3179,Px=163f,Py=156f}, A3=new ArtAnchor{Hip=746,Px=73f,Py=243f} },
            new ConstellationArt { Abbreviation="Cen", PngName="centaurus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=68933,Px=118f,Py=157f}, A2=new ArtAnchor{Hip=71683,Px=194f,Py=444f}, A3=new ArtAnchor{Hip=56561,Px=463f,Py=412f} },
            new ConstellationArt { Abbreviation="Cep", PngName="cepheus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=116727,Px=60f,Py=335f}, A2=new ArtAnchor{Hip=106032,Px=125f,Py=170f}, A3=new ArtAnchor{Hip=109492,Px=335f,Py=147f} },
            new ConstellationArt { Abbreviation="Com", PngName="coma-berenices", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=64241,Px=27f,Py=199f}, A2=new ArtAnchor{Hip=64394,Px=38f,Py=58f}, A3=new ArtAnchor{Hip=60742,Px=172f,Py=64f} },
            new ConstellationArt { Abbreviation="CVn", PngName="canes-venatici", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=63901,Px=117f,Py=113f}, A2=new ArtAnchor{Hip=61317,Px=206f,Py=40f}, A3=new ArtAnchor{Hip=61309,Px=207f,Py=152f} },
            new ConstellationArt { Abbreviation="Aur", PngName="auriga", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=28380,Px=196f,Py=189f}, A2=new ArtAnchor{Hip=24608,Px=419f,Py=208f}, A3=new ArtAnchor{Hip=23015,Px=290f,Py=423f} },
            new ConstellationArt { Abbreviation="Col", PngName="columba", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=30277,Px=139f,Py=29f}, A2=new ArtAnchor{Hip=28328,Px=40f,Py=190f}, A3=new ArtAnchor{Hip=25859,Px=204f,Py=211f} },
            new ConstellationArt { Abbreviation="Cir", PngName="circinus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=75323,Px=5f,Py=36f}, A2=new ArtAnchor{Hip=74824,Px=39f,Py=11f}, A3=new ArtAnchor{Hip=71908,Px=235f,Py=239f} },
            new ConstellationArt { Abbreviation="Crt", PngName="crater", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=58188,Px=45f,Py=41f}, A2=new ArtAnchor{Hip=55282,Px=160f,Py=149f}, A3=new ArtAnchor{Hip=54682,Px=55f,Py=242f} },
            new ConstellationArt { Abbreviation="CrA", PngName="corona-australis", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=93825,Px=14f,Py=142f}, A2=new ArtAnchor{Hip=90887,Px=218f,Py=34f}, A3=new ArtAnchor{Hip=92953,Px=195f,Py=208f} },
            new ConstellationArt { Abbreviation="CrB", PngName="corona-borealis", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=78493,Px=16f,Py=186f}, A2=new ArtAnchor{Hip=76952,Px=201f,Py=203f}, A3=new ArtAnchor{Hip=76127,Px=157f,Py=20f} },
            new ConstellationArt { Abbreviation="Crv", PngName="corvus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=60965,Px=65f,Py=59f}, A2=new ArtAnchor{Hip=61359,Px=77f,Py=235f}, A3=new ArtAnchor{Hip=59316,Px=213f,Py=187f} },
            new ConstellationArt { Abbreviation="Cru", PngName="crux", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=61084,Px=112f,Py=21f}, A2=new ArtAnchor{Hip=62434,Px=42f,Py=20f}, A3=new ArtAnchor{Hip=60718,Px=21f,Py=106f} },
            new ConstellationArt { Abbreviation="Cyg", PngName="cygnus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=107310,Px=7f,Py=382f}, A2=new ArtAnchor{Hip=94779,Px=474f,Py=46f}, A3=new ArtAnchor{Hip=95947,Px=467f,Py=453f} },
            new ConstellationArt { Abbreviation="Del", PngName="delphinus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=102532,Px=59f,Py=31f}, A2=new ArtAnchor{Hip=102805,Px=75f,Py=148f}, A3=new ArtAnchor{Hip=101421,Px=211f,Py=143f} },
            new ConstellationArt { Abbreviation="Dor", PngName="dorado", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=27890,Px=56f,Py=197f}, A2=new ArtAnchor{Hip=27100,Px=75f,Py=228f}, A3=new ArtAnchor{Hip=19893,Px=219f,Py=46f} },
            new ConstellationArt { Abbreviation="Dra", PngName="draco", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=56211,Px=13f,Py=411f}, A2=new ArtAnchor{Hip=85670,Px=361f,Py=154f}, A3=new ArtAnchor{Hip=97433,Px=449f,Py=429f} },
            new ConstellationArt { Abbreviation="Nor", PngName="norma", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=78639,Px=17f,Py=45f}, A2=new ArtAnchor{Hip=79509,Px=101f,Py=38f}, A3=new ArtAnchor{Hip=80582,Px=16f,Py=112f} },
            new ConstellationArt { Abbreviation="Eri", PngName="eridanus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=22109,Px=41f,Py=95f}, A2=new ArtAnchor{Hip=13701,Px=296f,Py=44f}, A3=new ArtAnchor{Hip=7588,Px=493f,Py=490f} },
            new ConstellationArt { Abbreviation="Sge", PngName="sagitta", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=98920,Px=6f,Py=12f}, A2=new ArtAnchor{Hip=96757,Px=249f,Py=213f}, A3=new ArtAnchor{Hip=96837,Px=218f,Py=243f} },
            new ConstellationArt { Abbreviation="For", PngName="fornax", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=14879,Px=42f,Py=125f}, A2=new ArtAnchor{Hip=13202,Px=128f,Py=37f}, A3=new ArtAnchor{Hip=13147,Px=190f,Py=129f} },
            new ConstellationArt { Abbreviation="Gem", PngName="gemini", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=37740,Px=14f,Py=81f}, A2=new ArtAnchor{Hip=32362,Px=117f,Py=252f}, A3=new ArtAnchor{Hip=28734,Px=249f,Py=165f} },
            new ConstellationArt { Abbreviation="Cam", PngName="camelopardalis", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=25110,Px=77f,Py=46f}, A2=new ArtAnchor{Hip=22783,Px=128f,Py=135f}, A3=new ArtAnchor{Hip=16228,Px=219f,Py=136f} },
            new ConstellationArt { Abbreviation="CMa", PngName="canis-major", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=35904,Px=21f,Py=347f}, A2=new ArtAnchor{Hip=33160,Px=321f,Py=24f}, A3=new ArtAnchor{Hip=30122,Px=318f,Py=492f} },
            new ConstellationArt { Abbreviation="UMa", PngName="ursa-major", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=67301,Px=26f,Py=75f}, A2=new ArtAnchor{Hip=41704,Px=452f,Py=272f}, A3=new ArtAnchor{Hip=50372,Px=258f,Py=394f} },
            new ConstellationArt { Abbreviation="Gru", PngName="grus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=114131,Px=49f,Py=109f}, A2=new ArtAnchor{Hip=112623,Px=85f,Py=206f}, A3=new ArtAnchor{Hip=108085,Px=220f,Py=45f} },
            new ConstellationArt { Abbreviation="Her", PngName="hercules", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=80170,Px=44f,Py=115f}, A2=new ArtAnchor{Hip=79992,Px=254f,Py=477f}, A3=new ArtAnchor{Hip=88794,Px=441f,Py=102f} },
            new ConstellationArt { Abbreviation="Hor", PngName="horlogium", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=19747,Px=16f,Py=235f}, A2=new ArtAnchor{Hip=12484,Px=184f,Py=67f}, A3=new ArtAnchor{Hip=14240,Px=235f,Py=119f} },
            new ConstellationArt { Abbreviation="Hya", PngName="hydra", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=64962,Px=26f,Py=491f}, A2=new ArtAnchor{Hip=55434,Px=111f,Py=149f}, A3=new ArtAnchor{Hip=43813,Px=401f,Py=33f} },
            new ConstellationArt { Abbreviation="Hyi", PngName="hydrus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=9236,Px=14f,Py=204f}, A2=new ArtAnchor{Hip=2021,Px=201f,Py=43f}, A3=new ArtAnchor{Hip=17678,Px=241f,Py=215f} },
            new ConstellationArt { Abbreviation="Ind", PngName="indus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=101772,Px=64f,Py=143f}, A2=new ArtAnchor{Hip=103227,Px=154f,Py=20f}, A3=new ArtAnchor{Hip=105319,Px=175f,Py=98f} },
            new ConstellationArt { Abbreviation="Lac", PngName="lacerta", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=110538,Px=249f,Py=42f}, A2=new ArtAnchor{Hip=111104,Px=108f,Py=123f}, A3=new ArtAnchor{Hip=109937,Px=67f,Py=215f} },
            new ConstellationArt { Abbreviation="Mon", PngName="monoceros", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=39863,Px=20f,Py=306f}, A2=new ArtAnchor{Hip=31978,Px=394f,Py=80f}, A3=new ArtAnchor{Hip=29651,Px=509f,Py=359f} },
            new ConstellationArt { Abbreviation="Lep", PngName="lepus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=28910,Px=21f,Py=48f}, A2=new ArtAnchor{Hip=24244,Px=246f,Py=62f}, A3=new ArtAnchor{Hip=23685,Px=213f,Py=235f} },
            new ConstellationArt { Abbreviation="Leo", PngName="leo", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=57632,Px=69f,Py=411f}, A2=new ArtAnchor{Hip=49669,Px=383f,Py=186f}, A3=new ArtAnchor{Hip=47908,Px=321f,Py=32f} },
            new ConstellationArt { Abbreviation="Lup", PngName="lupus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=74395,Px=90f,Py=257f}, A2=new ArtAnchor{Hip=70576,Px=207f,Py=435f}, A3=new ArtAnchor{Hip=75177,Px=425f,Py=239f} },
            new ConstellationArt { Abbreviation="Lyn", PngName="lynx", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=44248,Px=100f,Py=216f}, A2=new ArtAnchor{Hip=36145,Px=337f,Py=212f}, A3=new ArtAnchor{Hip=30060,Px=472f,Py=119f} },
            new ConstellationArt { Abbreviation="Lyr", PngName="lyra", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=92791,Px=100f,Py=96f}, A2=new ArtAnchor{Hip=91262,Px=157f,Py=38f}, A3=new ArtAnchor{Hip=92420,Px=139f,Py=162f} },
            new ConstellationArt { Abbreviation="Ant", PngName="antlia", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=51172,Px=4f,Py=84f}, A2=new ArtAnchor{Hip=47758,Px=72f,Py=69f}, A3=new ArtAnchor{Hip=48926,Px=42f,Py=120f} },
            new ConstellationArt { Abbreviation="Mic", PngName="microscopium", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=105140,Px=122f,Py=226f}, A2=new ArtAnchor{Hip=103738,Px=168f,Py=148f}, A3=new ArtAnchor{Hip=102831,Px=234f,Py=115f} },
            new ConstellationArt { Abbreviation="Mus", PngName="musca", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=61199,Px=57f,Py=52f}, A2=new ArtAnchor{Hip=62322,Px=177f,Py=68f}, A3=new ArtAnchor{Hip=57363,Px=129f,Py=236f} },
            new ConstellationArt { Abbreviation="Oct", PngName="octans", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=70638,Px=15f,Py=16f}, A2=new ArtAnchor{Hip=107089,Px=236f,Py=239f}, A3=new ArtAnchor{Hip=112405,Px=247f,Py=140f} },
            new ConstellationArt { Abbreviation="Aps", PngName="apus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=81852,Px=66f,Py=60f}, A2=new ArtAnchor{Hip=81065,Px=76f,Py=87f}, A3=new ArtAnchor{Hip=72370,Px=163f,Py=110f} },
            new ConstellationArt { Abbreviation="Oph", PngName="ophiuchus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=92946,Px=5f,Py=183f}, A2=new ArtAnchor{Hip=77233,Px=452f,Py=47f}, A3=new ArtAnchor{Hip=85755,Px=238f,Py=453f} },
            new ConstellationArt { Abbreviation="Ori", PngName="orion", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=27913,Px=59f,Py=11f}, A2=new ArtAnchor{Hip=27366,Px=329f,Py=477f}, A3=new ArtAnchor{Hip=22449,Px=421f,Py=91f} },
            new ConstellationArt { Abbreviation="Pav", PngName="pavo", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=100751,Px=55f,Py=60f}, A2=new ArtAnchor{Hip=98495,Px=110f,Py=208f}, A3=new ArtAnchor{Hip=86929,Px=235f,Py=146f} },
            new ConstellationArt { Abbreviation="Peg", PngName="pegasus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=107315,Px=164f,Py=41f}, A2=new ArtAnchor{Hip=109410,Px=47f,Py=283f}, A3=new ArtAnchor{Hip=1067,Px=409f,Py=349f} },
            new ConstellationArt { Abbreviation="Pic", PngName="pictor", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=32607,Px=4f,Py=252f}, A2=new ArtAnchor{Hip=27530,Px=148f,Py=138f}, A3=new ArtAnchor{Hip=27321,Px=167f,Py=38f} },
            new ConstellationArt { Abbreviation="Per", PngName="perseus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=18532,Px=164f,Py=323f}, A2=new ArtAnchor{Hip=15863,Px=299f,Py=175f}, A3=new ArtAnchor{Hip=13254,Px=385f,Py=386f} },
            new ConstellationArt { Abbreviation="Equ", PngName="equuleus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=104521,Px=17f,Py=180f}, A2=new ArtAnchor{Hip=104987,Px=149f,Py=86f}, A3=new ArtAnchor{Hip=105570,Px=160f,Py=160f} },
            new ConstellationArt { Abbreviation="CMi", PngName="canis-minor", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=37921,Px=96f,Py=43f}, A2=new ArtAnchor{Hip=37279,Px=101f,Py=171f}, A3=new ArtAnchor{Hip=36188,Px=184f,Py=124f} },
            new ConstellationArt { Abbreviation="LMi", PngName="leo-minor", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=53229,Px=8f,Py=40f}, A2=new ArtAnchor{Hip=46952,Px=214f,Py=127f}, A3=new ArtAnchor{Hip=50303,Px=61f,Py=155f} },
            new ConstellationArt { Abbreviation="Vul", PngName="vulpecula", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=98543,Px=35f,Py=133f}, A2=new ArtAnchor{Hip=95771,Px=174f,Py=111f}, A3=new ArtAnchor{Hip=94703,Px=242f,Py=135f} },
            new ConstellationArt { Abbreviation="UMi", PngName="ursa-minor", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=11767,Px=15f,Py=20f}, A2=new ArtAnchor{Hip=59504,Px=193f,Py=51f}, A3=new ArtAnchor{Hip=79822,Px=93f,Py=209f} },
            new ConstellationArt { Abbreviation="Phe", PngName="phoenix", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=8837,Px=55f,Py=243f}, A2=new ArtAnchor{Hip=765,Px=209f,Py=15f}, A3=new ArtAnchor{Hip=5348,Px=229f,Py=214f} },
            new ConstellationArt { Abbreviation="Psc", PngName="pisces", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=4889,Px=24f,Py=104f}, A2=new ArtAnchor{Hip=9487,Px=111f,Py=489f}, A3=new ArtAnchor{Hip=114971,Px=481f,Py=155f} },
            new ConstellationArt { Abbreviation="PsA", PngName="piscis-austrinus", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=113246,Px=190f,Py=5f}, A2=new ArtAnchor{Hip=107608,Px=82f,Py=202f}, A3=new ArtAnchor{Hip=111954,Px=236f,Py=92f} },
            new ConstellationArt { Abbreviation="Vol", PngName="volans", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=44382,Px=55f,Py=85f}, A2=new ArtAnchor{Hip=35228,Px=210f,Py=122f}, A3=new ArtAnchor{Hip=34481,Px=207f,Py=163f} },
            new ConstellationArt { Abbreviation="Ret", PngName="reticulum", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=18597,Px=47f,Py=56f}, A2=new ArtAnchor{Hip=19780,Px=78f,Py=88f}, A3=new ArtAnchor{Hip=19921,Px=16f,Py=111f} },
            new ConstellationArt { Abbreviation="Sgr", PngName="sagittarius", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=95168,Px=96f,Py=82f}, A2=new ArtAnchor{Hip=95294,Px=307f,Py=492f}, A3=new ArtAnchor{Hip=87072,Px=506f,Py=100f} },
            new ConstellationArt { Abbreviation="Sco", PngName="scorpius", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=78820,Px=447f,Py=29f}, A2=new ArtAnchor{Hip=85927,Px=62f,Py=365f}, A3=new ArtAnchor{Hip=82729,Px=217f,Py=462f} },
            new ConstellationArt { Abbreviation="Sex", PngName="sextans", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=51437,Px=26f,Py=54f}, A2=new ArtAnchor{Hip=49641,Px=68f,Py=36f}, A3=new ArtAnchor{Hip=48437,Px=118f,Py=84f} },
            new ConstellationArt { Abbreviation="Men", PngName="mensa", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=21949,Px=100f,Py=67f}, A2=new ArtAnchor{Hip=25918,Px=179f,Py=136f}, A3=new ArtAnchor{Hip=29134,Px=54f,Py=183f} },
            new ConstellationArt { Abbreviation="Tau", PngName="taurus", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=26451,Px=13f,Py=92f}, A2=new ArtAnchor{Hip=15900,Px=399f,Py=438f}, A3=new ArtAnchor{Hip=17999,Px=382f,Py=192f} },
            new ConstellationArt { Abbreviation="Tel", PngName="telescopium", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=91589,Px=30f,Py=66f}, A2=new ArtAnchor{Hip=90422,Px=101f,Py=40f}, A3=new ArtAnchor{Hip=90568,Px=106f,Py=92f} },
            new ConstellationArt { Abbreviation="Tuc", PngName="tucana", ImageW=256, ImageH=256, A1=new ArtAnchor{Hip=2484,Px=53f,Py=121f}, A2=new ArtAnchor{Hip=114996,Px=154f,Py=86f}, A3=new ArtAnchor{Hip=110130,Px=224f,Py=141f} },
            new ConstellationArt { Abbreviation="Tri", PngName="triangulum", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=10064,Px=16f,Py=42f}, A2=new ArtAnchor{Hip=10559,Px=13f,Py=70f}, A3=new ArtAnchor{Hip=8796,Px=97f,Py=78f} },
            new ConstellationArt { Abbreviation="TrA", PngName="triangulum-australe", ImageW=128, ImageH=128, A1=new ArtAnchor{Hip=77952,Px=26f,Py=12f}, A2=new ArtAnchor{Hip=74946,Px=108f,Py=15f}, A3=new ArtAnchor{Hip=82273,Px=60f,Py=106f} },
            new ConstellationArt { Abbreviation="Aqr", PngName="aquarius", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=115438,Px=144f,Py=464f}, A2=new ArtAnchor{Hip=109074,Px=179f,Py=98f}, A3=new ArtAnchor{Hip=102618,Px=465f,Py=49f} },
            new ConstellationArt { Abbreviation="Vir", PngName="virgo", ImageW=512, ImageH=512, A1=new ArtAnchor{Hip=72220,Px=65f,Py=389f}, A2=new ArtAnchor{Hip=57380,Px=454f,Py=57f}, A3=new ArtAnchor{Hip=65474,Px=338f,Py=382f} },
        };
    }
}
