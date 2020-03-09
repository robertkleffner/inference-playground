using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Basic
{
    public interface IType : IHasVariables<string>, ICanSubstitute<string, IType> { }

    public class TypeVariable : IType
    {
        public string Name { get; }

        public TypeVariable(string name) { this.Name = name; }

        public IImmutableSet<string> FreeVariables() =>
            ImmutableHashSet.Create(this.Name);

        public IType Substitute(string name, IType subWith) =>
            this.Name == name ? subWith : this;

        public override string ToString() => this.Name;

        public override bool Equals(object obj) => obj is TypeVariable other ? other.Name.Equals(this.Name) : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Name);
    }

    public class ArrowType : IType
    {
        public IType Input { get; }
        public IType Output { get; }

        public ArrowType(IType first, IType second, params IType[] rest)
        {
            if (!rest.Any())
            {
                this.Input = first;
                this.Output = second;
            }
            else
            {
                this.Input = first;
                this.Output = rest.Reverse().Append(second).Aggregate((outType, inType) => new ArrowType(inType, outType));
            }
        }

        public IImmutableSet<string> FreeVariables() =>
            this.Input.FreeVariables()
                .Union(this.Output.FreeVariables());

        public IType Substitute(string name, IType subWith) =>
            new ArrowType(this.Input.Substitute(name, subWith), this.Output.Substitute(name, subWith));

        public override string ToString() => $"({this.Input} -> {this.Output})";

        public override bool Equals(object obj) =>
            obj is ArrowType other
            ? other.Input.Equals(this.Input) && other.Output.Equals(this.Output)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Input).Hash(this.Output);
    }

    public class TypeScheme : IHasVariables<string>
    {
        public IReadOnlyList<string> Quantified { get; }
        public IType Body { get; }

        public TypeScheme(IType body, IReadOnlyList<string> quantified) { this.Body = body; this.Quantified = quantified; }

        public TypeScheme(IType body, params string[] quantified) { this.Body = body; this.Quantified = quantified; }

        public IImmutableSet<string> FreeVariables() =>
            this.Body.FreeVariables()
                .Except(this.Quantified);

        public override string ToString() => $"forall {string.Join(",", this.Quantified)}.{this.Body}";
    }
}
