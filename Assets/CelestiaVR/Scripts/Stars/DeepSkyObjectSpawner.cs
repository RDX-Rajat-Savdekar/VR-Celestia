using UnityEngine;
using CelestiaVR.Core;

namespace CelestiaVR.Stars
{
    /// <summary>
    /// Spawns billboard quads for deep sky objects at their correct RA/Dec positions
    /// on the sky sphere. Each object gets a CelestialBody so gaze/dwell inspection works.
    ///
    /// Textures are loaded from Assets/Resources/DSO/ at runtime so this works on Quest
    /// without any Inspector assignments. Objects with a specific photographic image use
    /// that; others fall back to a type-generic SGT texture (spiral galaxy, nebula, etc.).
    ///
    /// Attach to [StarField] (child of [SkyManager]).
    /// </summary>
    public class DeepSkyObjectSpawner : MonoBehaviour
    {
        // ── DSO type enum ─────────────────────────────────────────────────────────

        private enum DSOType
        {
            SpiralGalaxy,
            IrregularGalaxy,
            EllipticalGalaxy,
            Nebula,
            PlanetaryNebula,
            SupernovaRemnant,
            OpenCluster,
            GlobularCluster,
        }

        // ── Catalog entry ─────────────────────────────────────────────────────────

        private struct DSO
        {
            public string  name;
            public DSOType dsoType;
            public float   raHours;
            public float   decDegrees;
            public float   displaySize;         // sky-sphere units
            public string  description;
            public string  imageName;           // null = use type-generic SGT texture
            public float   magnitude;
            public float   distanceLightYears;
        }

        // ── 20-object catalog ─────────────────────────────────────────────────────

        private static readonly DSO[] Catalog = new DSO[]
        {
            new DSO {
                name="Andromeda Galaxy (M31)", dsoType=DSOType.SpiralGalaxy,
                raHours=0.712f, decDegrees=41.27f, displaySize=30f,
                imageName="andromeda", magnitude=3.44f, distanceLightYears=2_537_000f,
                description="The nearest large galaxy to our own, 2.5 million light-years away. " +
                            "Visible to the naked eye as a faint smudge — it contains roughly one trillion stars " +
                            "and is on a slow collision course with the Milky Way.",
            },
            new DSO {
                name="Orion Nebula (M42)", dsoType=DSOType.Nebula,
                raHours=5.590f, decDegrees=-5.38f, displaySize=20f,
                imageName="orion-nebula", magnitude=4.0f, distanceLightYears=1_344f,
                description="A stellar nursery 1,344 light-years away where new stars are being born right now. " +
                            "One of the most photographed objects in the sky and easily visible to the naked eye " +
                            "as the middle 'star' in Orion's sword.",
            },
            new DSO {
                name="Pleiades (M45)", dsoType=DSOType.OpenCluster,
                raHours=3.790f, decDegrees=24.12f, displaySize=22f,
                imageName="pleiades", magnitude=1.6f, distanceLightYears=444f,
                description="An open star cluster 444 light-years away, also called the Seven Sisters. " +
                            "Most people can spot 6 stars with the naked eye on a clear night. " +
                            "The blue nebulosity is a dust cloud the cluster is passing through.",
            },
            new DSO {
                name="Hercules Cluster (M13)", dsoType=DSOType.GlobularCluster,
                raHours=16.694f, decDegrees=36.46f, displaySize=13f,
                imageName="hercules-cluster", magnitude=5.8f, distanceLightYears=25_100f,
                description="A globular cluster 25,000 light-years away containing roughly 300,000 stars " +
                            "packed into a sphere 150 light-years across. In 1974 it was the target of the " +
                            "Arecibo message — humanity's first deliberate radio signal to the stars.",
            },
            new DSO {
                name="Eagle Nebula (M16)", dsoType=DSOType.Nebula,
                raHours=18.313f, decDegrees=-13.80f, displaySize=16f,
                imageName="eagle-nebula", magnitude=6.0f, distanceLightYears=7_000f,
                description="Home of the famous 'Pillars of Creation' — towering columns of gas and dust " +
                            "7,000 light-years away where new stars are actively forming. " +
                            "Photographed in stunning detail by the Hubble Space Telescope in 1995.",
            },
            new DSO {
                name="Triangulum Galaxy (M33)", dsoType=DSOType.SpiralGalaxy,
                raHours=1.567f, decDegrees=30.66f, displaySize=18f,
                imageName="triangulum", magnitude=5.7f, distanceLightYears=2_730_000f,
                description="The third-largest member of our Local Group, 2.73 million light-years away. " +
                            "A face-on spiral with loose, patchy arms containing about 40 billion stars.",
            },
            new DSO {
                name="Whirlpool Galaxy (M51)", dsoType=DSOType.SpiralGalaxy,
                raHours=13.498f, decDegrees=47.20f, displaySize=13f,
                imageName="whirlpool", magnitude=8.4f, distanceLightYears=23_000_000f,
                description="A grand-design spiral 23 million light-years away, locked in a gravitational " +
                            "tug-of-war with its smaller companion NGC 5195. Its tightly wound spiral arms " +
                            "are a textbook example of galactic structure.",
            },
            new DSO {
                name="Sombrero Galaxy (M104)", dsoType=DSOType.SpiralGalaxy,
                raHours=12.667f, decDegrees=-11.62f, displaySize=12f,
                imageName="sombrero", magnitude=8.0f, distanceLightYears=29_300_000f,
                description="Named for its broad rim and central bulge resembling a wide-brimmed hat, " +
                            "the Sombrero sits 29 million light-years away. Its dark dust lane and massive " +
                            "bulge suggest a supermassive black hole at its core.",
            },
            new DSO {
                name="Black Eye Galaxy (M64)", dsoType=DSOType.SpiralGalaxy,
                raHours=12.945f, decDegrees=21.68f, displaySize=11f,
                imageName="black-eye", magnitude=8.5f, distanceLightYears=24_000_000f,
                description="A dark band of dust in front of M64's bright nucleus gives it the appearance of " +
                            "a black eye. Unusually, its inner stars rotate opposite to its outer stars — " +
                            "a relic of an ancient galaxy merger.",
            },
            new DSO {
                name="Bode's Galaxy (M81)", dsoType=DSOType.SpiralGalaxy,
                raHours=9.926f, decDegrees=69.07f, displaySize=15f,
                imageName="bodes-galaxy", magnitude=6.9f, distanceLightYears=11_740_000f,
                description="One of the brightest galaxies in the northern sky at 11.7 million light-years. " +
                            "M81 interacts gravitationally with its neighbour M82, driving intense " +
                            "star formation activity in that galaxy.",
            },
            new DSO {
                name="Cigar Galaxy (M82)", dsoType=DSOType.IrregularGalaxy,
                raHours=9.928f, decDegrees=69.68f, displaySize=13f,
                imageName="cigar-galaxy", magnitude=8.4f, distanceLightYears=11_400_000f,
                description="A starburst galaxy 11.4 million light-years away being tidally disrupted by M81. " +
                            "Superwinds of gas are being expelled from its core at 2 million km/h, " +
                            "visible as red filaments in long-exposure images.",
            },
            new DSO {
                name="Ring Nebula (M57)", dsoType=DSOType.PlanetaryNebula,
                raHours=18.893f, decDegrees=33.03f, displaySize=9f,
                imageName="ring-nebula", magnitude=8.8f, distanceLightYears=2_300f,
                description="A classic planetary nebula — the shell of gas ejected by a dying Sun-like star " +
                            "2,300 light-years away. The glowing ring is about one light-year across and the " +
                            "faint white dwarf at its centre is slowly cooling toward darkness.",
            },
            new DSO {
                name="Dumbbell Nebula (M27)", dsoType=DSOType.PlanetaryNebula,
                raHours=19.994f, decDegrees=22.72f, displaySize=11f,
                imageName="dumbbell-nebula", magnitude=7.4f, distanceLightYears=1_360f,
                description="The first planetary nebula ever discovered (1764), 1,360 light-years away. " +
                            "Its apple-core shape is created by gas expelled in opposite directions by the " +
                            "central white dwarf. Larger than the Ring Nebula and easier to see.",
            },
            new DSO {
                name="Lagoon Nebula (M8)", dsoType=DSOType.Nebula,
                raHours=18.063f, decDegrees=-24.38f, displaySize=20f,
                imageName="lagoon-nebula", magnitude=6.0f, distanceLightYears=4_100f,
                description="A vast emission nebula and star-forming region 4,100 light-years away in Sagittarius. " +
                            "The dark lane cutting through its glowing hydrogen gas gives it the name 'Lagoon'. " +
                            "Visible to the naked eye from dark skies.",
            },
            new DSO {
                name="Crab Nebula (M1)", dsoType=DSOType.SupernovaRemnant,
                raHours=5.575f, decDegrees=22.01f, displaySize=11f,
                imageName="crab-nebula", magnitude=8.4f, distanceLightYears=6_500f,
                description="The remnant of a supernova witnessed by Chinese astronomers in 1054 AD. " +
                            "At its heart spins a pulsar — a neutron star rotating 30 times per second, " +
                            "powering the nebula's eerie blue glow with its magnetic field.",
            },
            new DSO {
                name="Beehive Cluster (M44)", dsoType=DSOType.OpenCluster,
                raHours=8.667f, decDegrees=19.67f, displaySize=22f,
                imageName="beehive-cluster", magnitude=3.7f, distanceLightYears=577f,
                description="One of the nearest open clusters at 577 light-years, visible to the naked eye " +
                            "as a faint fuzzy patch in Cancer. Contains over 1,000 confirmed member stars " +
                            "spread across 23 light-years.",
            },
            new DSO {
                name="Double Cluster (NGC 869/884)", dsoType=DSOType.OpenCluster,
                raHours=2.320f, decDegrees=57.13f, displaySize=20f,
                imageName="double-cluster", magnitude=5.3f, distanceLightYears=7_500f,
                description="A pair of young open clusters in Perseus, each containing hundreds of bright " +
                            "blue-white supergiant stars. They formed around the same time from the same " +
                            "giant molecular cloud, 7,500 light-years away.",
            },
            new DSO {
                name="Omega Centauri (NGC 5139)", dsoType=DSOType.GlobularCluster,
                raHours=13.447f, decDegrees=-47.48f, displaySize=17f,
                imageName="omega-centauri", magnitude=3.9f, distanceLightYears=17_090f,
                description="The largest and most massive globular cluster in the Milky Way, containing " +
                            "roughly 10 million stars. At 17,000 light-years it is visible to the naked eye " +
                            "and may be the stripped core of a dwarf galaxy consumed long ago.",
            },
            new DSO {
                name="Large Magellanic Cloud", dsoType=DSOType.IrregularGalaxy,
                raHours=5.370f, decDegrees=-69.73f, displaySize=40f,
                imageName="lmc", magnitude=0.9f, distanceLightYears=163_000f,
                description="A satellite galaxy of the Milky Way 163,000 light-years away, visible only " +
                            "from the southern hemisphere. It contains about 30 billion stars and in 1987 " +
                            "hosted a supernova visible to the naked eye — the closest in 400 years.",
            },
            new DSO {
                name="Small Magellanic Cloud", dsoType=DSOType.IrregularGalaxy,
                raHours=0.870f, decDegrees=-72.83f, displaySize=22f,
                imageName="smc", magnitude=2.7f, distanceLightYears=200_000f,
                description="The smaller companion galaxy to the Milky Way, 200,000 light-years away. " +
                            "Despite its modest appearance it contains several hundred million stars and " +
                            "is being slowly torn apart by the Milky Way's gravity.",
            },
        };

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            var skyManager = GetComponentInParent<SkyManager>();
            float radius   = skyManager != null ? skyManager.skyRadius - 5f : 495f;

            foreach (var dso in Catalog)
                SpawnBillboard(dso, radius);
        }

        // ── Internal ─────────────────────────────────────────────────────────────

        private void SpawnBillboard(DSO dso, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = dso.name;
            go.transform.SetParent(transform, false);

            Vector3 localPos = CelestialCoordinates.RADecToUnity(dso.raHours, dso.decDegrees, radius);
            go.transform.localPosition = localPos;
            go.transform.localScale    = Vector3.one * dso.displaySize;
            go.transform.localRotation = Quaternion.LookRotation(-localPos.normalized);

            Destroy(go.GetComponent<MeshCollider>());
            var col    = go.AddComponent<SphereCollider>();
            col.radius = 0.5f;

            var r   = go.GetComponent<MeshRenderer>();
            var tex = LoadTexture(dso);

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color");
            var mat   = new Material(sh) { name = dso.name + "_Mat" };
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.SetTexture("_BaseMap", tex);
            }
            // Additive blending — black background vanishes, bright nebula/galaxy glows
            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_Blend",    0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite",   0f);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            r.material          = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows    = false;

            var body                  = go.AddComponent<CelestialBody>();
            body.objectName           = dso.name;
            body.bodyType             = CelestialBodyType.DeepSkyObject;
            body.dsoSubType           = dso.dsoType.ToString();
            body.description          = dso.description;
            body.rightAscensionHours  = dso.raHours;
            body.declinationDegrees   = dso.decDegrees;
            body.magnitude            = dso.magnitude;
            body.distanceLightYears   = dso.distanceLightYears;
        }

        /// <summary>
        /// Loads the object-specific photographic image from Resources/DSO/ if available,
        /// otherwise returns the type-generic SGT texture for this DSO category.
        /// </summary>
        private static Texture2D LoadTexture(DSO dso)
        {
            // Images live at Resources/DSO/{imageName}/{imageName}.jpg
            if (!string.IsNullOrEmpty(dso.imageName))
            {
                var tex = Resources.Load<Texture2D>("DSO/" + dso.imageName + "/" + dso.imageName);
                if (tex != null) return tex;
                Debug.LogWarning($"[DeepSkyObjectSpawner] 'DSO/{dso.imageName}/{dso.imageName}' not found, using type fallback.");
            }

            string fallback = dso.dsoType switch
            {
                DSOType.SpiralGalaxy     => "DSO/sgt-spiral-galaxy/SpiralGalaxy",
                DSOType.EllipticalGalaxy => "DSO/sgt-spiral-galaxy/SpiralGalaxy",
                DSOType.IrregularGalaxy  => "DSO/sgt-irregular-galaxy/Galaxy_Dust",
                DSOType.Nebula           => "DSO/sgt-nebula/Nebula",
                DSOType.PlanetaryNebula  => "DSO/sgt-nebula/Nebula",
                DSOType.SupernovaRemnant => "DSO/sgt-nebula/Nebula",
                _                        => "DSO/sgt-irregular-galaxy/Galaxy_Dust",
            };

            var fallbackTex = Resources.Load<Texture2D>(fallback);
            if (fallbackTex == null)
                Debug.LogWarning($"[DeepSkyObjectSpawner] Fallback '{fallback}' not found. Check Assets/Resources/DSO/.");
            return fallbackTex;
        }
    }
}
