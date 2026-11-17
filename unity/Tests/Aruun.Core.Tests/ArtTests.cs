using System.Collections.Generic;
using System.IO;
using Aruun.Core.Art;
using NUnit.Framework;

namespace Aruun.Core.Tests
{
    public class ArtTests
    {
        static ModelCatalog Catalog()
        {
            return ModelCatalog.FromJson(File.ReadAllText(Path.Combine(TestContent.Root, "models", "models.json")),
                File.ReadAllText(Path.Combine(TestContent.Root, "art", "palette.json")));
        }

        static VoxFile Model(ModelCatalog cat, string id)
        {
            return VoxFile.Load(Path.Combine(TestContent.Root, "models", cat.Get(id).File));
        }

        [Test]
        public void EveryModelLoadsAndMeshes()
        {
            var cat = Catalog();
            Assert.That(cat.Count, Is.GreaterThan(50));
            foreach (string id in cat.Ids)
            {
                var file = Model(cat, id);
                Assert.That(file.HasPalette, id);
                var mesher = new ModelMesher();
                foreach (var m in file.Models) mesher.Add(m, file.Palette, cat.GlowFlags(file), 0, 0, 0);
                Assert.That(mesher.Data.IsEmpty, Is.False, id);
                string cat2 = cat.Get(id).Category;
                if (cat2 == "creatures" || cat2 == "props")   // items hang from their grip; crop roots dip
                    Assert.That(mesher.Data.MinY, Is.GreaterThanOrEqualTo(-1f), id + " should stand on its origin");
            }
        }

        [Test]
        public void RigPartsMatchObjectsInTheFile()
        {
            var cat = Catalog();
            int rigged = 0;
            foreach (string id in cat.Ids)
            {
                var info = cat.Get(id);
                if (!info.IsRigged) continue;
                rigged++;
                var file = Model(cat, id);
                foreach (var p in info.Parts)
                    Assert.That(file.Find(p.Name), Is.Not.Null, id + " part " + p.Name);
                foreach (var clip in info.Clips)
                    foreach (var ch in clip.Value)
                        Assert.That(info.Parts.Exists(p => p.Name == ch.Part), id + " clip " + clip.Key + " names " + ch.Part);
                for (int i = 0; i < info.Parts.Count; i++) // parents come first
                    if (info.Parts[i].Parent != "")
                        Assert.That(info.Parts.FindIndex(p => p.Name == info.Parts[i].Parent), Is.LessThan(i));
            }
            Assert.That(rigged, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void ModelsFaceForwardAndStandUp()
        {
            // The redstrider's head is in front of its body (Unity +z) and above it.
            var info = Catalog().Get("redstrider");
            var head = info.Parts.Find(p => p.Name == "head");
            var tail = info.Parts.Find(p => p.Name == "tail");
            Assert.That(head.PivotZ, Is.GreaterThan(tail.PivotZ));
            var file = Model(Catalog(), "redstrider");
            var h = file.Find("head");
            Assert.That(h.OriginZ + h.SizeZ / 2f, Is.GreaterThan(file.Find("tail").OriginZ));
            Assert.That(h.OriginY, Is.GreaterThan(30));
        }

        [Test]
        public void ClipsSwingWithUnityHandedness()
        {
            var clip = new List<RigChannel>
            {
                new RigChannel { Part = "arm", Axis = 'x', Amp = 0, Offset = 30 },
                new RigChannel { Part = "arm", Axis = 'z', Amp = 0, Offset = 10 },
                new RigChannel { Part = "body", Amp = 0, Bob = 2, Freq = 1, Phase = 0.25f },
            };
            var pose = new Dictionary<string, PartPose>();
            RigPose.Evaluate(clip, 0, pose);
            Assert.That(pose["arm"].X, Is.EqualTo(-30).Within(1e-4));
            Assert.That(pose["arm"].Z, Is.EqualTo(10).Within(1e-4));
            Assert.That(pose["body"].Lift, Is.EqualTo(2).Within(1e-4));
        }

        [Test]
        public void GlowColoursAreFound()
        {
            var cat = Catalog();
            var file = Model(cat, "word_fragment");
            var glow = cat.GlowFlags(file);
            var mesher = new ModelMesher();
            foreach (var m in file.Models) mesher.Add(m, file.Palette, glow, 0, 0, 0);
            Assert.That(mesher.Data.Glow.Count, Is.GreaterThan(0));
            Assert.That(mesher.Data.Solid.Count, Is.GreaterThan(0));
        }

        [Test]
        public void StructuresBecomeKnownBlocks()
        {
            var blocks = TestContent.Blocks();
            var dir = Path.Combine(TestContent.Root, "structures");
            int n = 0;
            foreach (string path in Directory.GetFiles(dir, "*.vox"))
            {
                var stamp = StructureStamp.FromVox(VoxFile.Load(path), blocks);
                Assert.That(stamp.UnknownColours, Is.Empty, Path.GetFileName(path));
                Assert.That(stamp.Voxels.Count, Is.GreaterThan(50), Path.GetFileName(path));
                int minY = int.MaxValue;
                foreach (var v in stamp.Voxels) { if (v.Y < minY) minY = v.Y; Assert.That(blocks.Get(v.Block).Solid); }
                Assert.That(minY, Is.EqualTo(0), Path.GetFileName(path) + " floor");
                n++;
            }
            Assert.That(n, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void RejectsFilesThatAreNotVox()
        {
            Assert.Throws<System.FormatException>(() => VoxFile.Parse(new byte[64]));
        }
    }
}
