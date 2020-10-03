using Microsoft.VisualStudio.TestTools.UnitTesting;

using Inference.Dots;
using System.Collections.Immutable;
using System.Linq;

namespace Inference.Dots.Test
{
    [TestClass]
    public class TypesTests
    {
        [TestMethod]
        public void DepthTest()
        {
            Assert.AreEqual(0, new TypeVariable("x", new DataKind()).Depth);
            Assert.AreEqual(2, new TypeSequence(new IType[] { new TypeSequence(new IType[0].ToImmutableList()) }.ToImmutableList()).Depth);
            Assert.AreEqual(1, new TypeSequence(new IType[] { new TypeApplication(new TypeSequence(new IType[0].ToImmutableList()), new TypeVariable("x", new DataKind()))}.ToImmutableList()).Depth);
        }

        [TestMethod]
        public void LevelsTest()
        {
            Assert.AreEqual(Enumerable.Empty<int>(), new TypeVariable("x", new DataKind()).Levels());
            CollectionAssert.AreEqual(new int[] { 0 }, new TypeSequence(new IType[0].ToImmutableList()).Levels().ToList());
            CollectionAssert.AreEqual(new int[] { 2, 1 }, new TypeSequence(new IType[]
            {
                new TypeSequence(new IType[]
                {
                    new TypeVariable("x", new DataKind())
                }.ToImmutableList()),
                new TypeSequence(new IType[]
                {
                    new TypeApplication(new TypeConstructor("K", new DataKind()), new TypeVariable("y", new DataKind()))
                }.ToImmutableList())
            }.ToImmutableList()).Levels().ToList());
        }
    }
}
