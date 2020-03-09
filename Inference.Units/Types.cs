using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Units
{
    public interface IType : IHasVariables<string>, ICanSubstitute<string, IType>
    {
        Unit ToUnit();
        FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints);
    }

    public class TypeVariable : IType
    {
        public string Name { get; }

        public TypeVariable(string name) { this.Name = name; }

        public IImmutableSet<string> FreeVariables() =>
            ImmutableHashSet.Create(this.Name);

        public IType Substitute(string name, IType subWith) =>
            this.Name == name ? subWith : this;

        public Unit ToUnit() => new Unit(this.Name);

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints)
        {
            hull = this;
            constraints = ImmutableList<(string name, IType replaced)>.Empty;
            return fresh;
        }

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

        public Unit ToUnit() => throw new Exception($"Attempt to convert non-unit type {this} to unit.");

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints)
        {
            var f1 = this.Input.MakeHull(fresh, out var leftHull, out var leftConstraints);
            var f2 = this.Output.MakeHull(f1, out var rightHull, out var rightConstraints);
            hull = new ArrowType(leftHull, rightHull);
            constraints = leftConstraints.AddRange(rightConstraints);
            return f2;
        }

        public override string ToString() => $"({this.Input} -> {this.Output})";

        public override bool Equals(object obj) =>
            obj is ArrowType other
            ? other.Input.Equals(this.Input) && other.Output.Equals(this.Output)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Input).Hash(this.Output);
    }

    public class FloatType : IType
    {
        public IType Unit { get; }

        public FloatType(IType unit) { this.Unit = unit; }

        public IImmutableSet<string> FreeVariables() => this.Unit.FreeVariables();

        public IType Substitute(string replace, IType replaceWith) => new FloatType(this.Unit.Substitute(replace, replaceWith).ToUnit().ToType());

        public Unit ToUnit() => throw new Exception($"Attempt to convert non-unit type {this} to unit.");

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints)
        {
            var newFresh = fresh.Next("h", out var hullFresh);
            hull = new FloatType(new TypeVariable(hullFresh));
            constraints = ImmutableList.Create((hullFresh, this.Unit));
            return newFresh;
        }

        public override string ToString() => $"F<{this.Unit.ToString()}>";

        public override bool Equals(object obj) => obj is FloatType other ? other.Unit.Equals(this.Unit) : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Unit);
    }

    public class UnitIdentity : IType
    {
        public UnitIdentity() { }

        public IImmutableSet<string> FreeVariables() => ImmutableHashSet<string>.Empty;

        public IType Substitute(string replace, IType replaceWith) => this;

        public Unit ToUnit() => new Unit();

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints) =>
            throw new InvalidOperationException("Unit types do not have a hull.");

        public override string ToString() => "1";

        public override bool Equals(object obj) => obj is UnitIdentity;

        public override int GetHashCode() => Hashing.Start;
    }

    public class UnitMultiply : IType
    {
        public IType Left { get; }
        public IType Right { get; }

        public UnitMultiply(IType left, IType right)
        {
            this.Left = left;
            this.Right = right;
        }

        public IImmutableSet<string> FreeVariables() =>
            this.Left.FreeVariables().Union(this.Right.FreeVariables());

        public IType Substitute(string replace, IType replaceWith) =>
            new UnitMultiply(this.Left.Substitute(replace, replaceWith), this.Right.Substitute(replace, replaceWith));

        public Unit ToUnit() => this.Left.ToUnit().Add(this.Right.ToUnit());

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints) =>
            throw new InvalidOperationException("Unit types do not have a hull.");

        public override string ToString() => $"{this.Left}*{this.Right}";

        public override bool Equals(object obj) =>
            obj is UnitMultiply other
            ? other.Left.Equals(this.Left) && other.Right.Equals(this.Right)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Left).Hash(this.Right);
    }

    public class UnitPower : IType
    {
        public IType Unit { get; }
        public int Exponent { get; }

        public UnitPower(IType unit, int exponent)
        {
            this.Unit = unit;
            this.Exponent = exponent;
        }

        public IImmutableSet<string> FreeVariables() => this.Unit.FreeVariables();

        public IType Substitute(string replace, IType replaceWith) =>
            new UnitPower(this.Unit.Substitute(replace, replaceWith), this.Exponent);

        public Unit ToUnit() => this.Unit.ToUnit().Scale(this.Exponent);

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints) =>
            throw new InvalidOperationException("Unit types do not have a hull.");

        public override string ToString() => $"({this.Unit}^{this.Exponent})";

        public override bool Equals(object obj) =>
            obj is UnitPower other
            ? other.Unit.Equals(this.Unit) && other.Exponent == this.Exponent
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Unit).Hash(this.Exponent);
    }

    public class PrimitiveUnit : IType
    {
        public string Name { get; }

        public PrimitiveUnit(string name)
        {
            this.Name = name;
        }

        public IImmutableSet<string> FreeVariables() => ImmutableHashSet<string>.Empty;

        public IType Substitute(string replace, IType replaceWith) => this;

        public Unit ToUnit() => new Unit(new Dictionary<string, int>(), new Dictionary<string, int>() { { this.Name, 1 } });

        public FreshVariableStream MakeHull(FreshVariableStream fresh, out IType hull, out IImmutableList<(string name, IType replaced)> constraints) =>
            throw new InvalidOperationException("Unit types do not have a hull.");

        public override string ToString() => this.Name;

        public override bool Equals(object obj) => obj is PrimitiveUnit other ? other.Name.Equals(this.Name) : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Name);
    }

    public class TypeScheme : IHasVariables<string>
    {
        public IReadOnlyList<(string name, Kind kind)> Quantified { get; }
        public IType Body { get; }

        public TypeScheme(IType body, IReadOnlyList<(string, Kind)> quantified) { this.Body = body; this.Quantified = quantified; }

        public TypeScheme(IType body, params (string, Kind)[] quantified) { this.Body = body; this.Quantified = quantified; }

        public IImmutableSet<string> FreeVariables() =>
            this.Body.FreeVariables()
                .Except(this.Quantified.Select(p => p.name));

        public override string ToString()
        {
            var prettyQuantified = this.Quantified.Select(p => $"{p.name}:{p.kind.Pretty()}");
            return $"∀ {string.Join(",", prettyQuantified)}.{this.Body}";
        }

        public override bool Equals(object obj) =>
            obj is TypeScheme other
            ? other.Body.Equals(this.Body) && other.Quantified.Zip(this.Quantified, (p1, p2) => p1.kind == p2.kind && p1.name == p2.name).All(b => b)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Quantified).Hash(this.Body);
    }
}
