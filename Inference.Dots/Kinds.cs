using System;
using System.Collections.Generic;
using System.Text;

using Inference.Common;

namespace Inference.Dots
{
    public interface IKind { }

    public class DataKind : IKind
    {
        public override string ToString() => "*";

        public override bool Equals(object obj) => obj is DataKind;

        public override int GetHashCode() => Hashing.Start.Hash("*");
    }

    public class ArrowKind : IKind
    {
        public IKind Func { get; }
        public IKind Arg { get; }

        public ArrowKind(IKind left, IKind right)
        {
            this.Func = left;
            this.Arg = right;
        }

        public override string ToString() => $"({this.Func} -> {this.Arg})";

        public override bool Equals(object obj) =>
            obj is ArrowKind arr
                ? arr.Func.Equals(this.Func) && arr.Arg.Equals(this.Arg)
                : false;

        public override int GetHashCode() => Hashing.Start.Hash("->").Hash(this.Func).Hash(this.Arg);
    }

    public class SequenceKind : IKind
    {

        public IKind Elem { get; }

        public SequenceKind(IKind elem)
        {
            this.Elem = elem;
        }

        public override string ToString() => $"[{this.Elem}]";

        public override bool Equals(object obj) =>
            obj is SequenceKind seq
                ? seq.Elem.Equals(this.Elem)
                : false;

        public override int GetHashCode() => Hashing.Start.Hash("[]").Hash(this.Elem);
    }

    public static class PredefinedKinds
    {
        public static readonly IKind OnlySequenceKind = new SequenceKind(new DataKind());
        public static readonly IKind DotsKind = new ArrowKind(new DataKind(), new DataKind());
    }
}
