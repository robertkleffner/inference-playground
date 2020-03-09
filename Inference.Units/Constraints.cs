using System;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Units
{
    public interface IConstraint : IContextEntry
    {
        InferenceState Solve(FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix);
    }

    public class TypeConstraint : IConstraint
    {
        public IImmutableList<IContextEntry> Dependencies { get; }
        public IType Left { get; }
        public IType Right { get; }

        public TypeConstraint(IType left, IType right)
        {
            this.Dependencies = ImmutableList.Create<IContextEntry>();
            this.Left = left;
            this.Right = right;
        }

        public TypeConstraint(IImmutableList<IContextEntry> dependencies, IType left, IType right)
        {
            this.Dependencies = dependencies;
            this.Left = left;
            this.Right = right;
        }

        public TypeConstraint Substitute(string name, IType subWith) =>
            new TypeConstraint(this.Left.Substitute(name, subWith), this.Right.Substitute(name, subWith));

        public override string ToString() => $"[{string.Join(", ", this.Dependencies.Select(c => c.ToString()))} | {this.Left} == {this.Right}]";

        public InferenceState Solve(FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            switch (this.Left, this.Right)
            {
                case (ArrowType aleft, ArrowType aright):
                    return new InferenceState(fresh, prefix
                        .Add(new TypeConstraint(aleft.Input, aright.Input))
                        .Add(new TypeConstraint(aleft.Output, aright.Output))
                        .AddRange(suffix));
                case (FloatType fleft, FloatType fright):
                    return new InferenceState(fresh, prefix
                        .Add(new UnitConstraint(fleft.Unit, fright.Unit))
                        .AddRange(suffix));
                case (TypeVariable vleft, TypeVariable vright):
                    var top = prefix[prefix.Count - 1];
                    var popped = prefix.RemoveAt(prefix.Count - 1);
                    return top switch
                    {
                        LocalityMarker _ => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        TermVariableBinding _ => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        TypeVariableIntro i => (vleft.Name == i.Name, vright.Name == i.Name) switch
                        {
                            (true, true) => new InferenceState(fresh, popped.Add(top).AddRange(suffix)),
                            (true, false) => new InferenceState(fresh, popped.Add(new TypeVariableDefinition(vleft.Name, vright, Kind.Value)).AddRange(suffix)),
                            (false, true) => new InferenceState(fresh, popped.Add(new TypeVariableDefinition(vright.Name, vleft, Kind.Value)).AddRange(suffix)),
                            (false, false) => new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix)),
                        },
                        TypeVariableDefinition d => (vleft.Name == d.Name, vright.Name == d.Name) switch
                        {
                            (true, true) => new InferenceState(fresh, popped.Add(top).AddRange(suffix)),
                            _ => new InferenceState(fresh, popped.Add(this.Substitute(d.Name, d.Definition)).Add(top).AddRange(suffix)),
                        },
                        _ => throw new Exception($"Unknown context entry: {top}."),
                    };
                case (TypeVariable vleft, IType tright): return this.DecomposeIntoHullConstraint(vleft, tright, fresh, prefix, suffix);
                case (IType tleft, TypeVariable vright): return this.DecomposeIntoHullConstraint(vright, tleft, fresh, prefix, suffix);
                default: throw new Exception("Rigid-rigid mismatch");
            }
        }

        private InferenceState DecomposeIntoHullConstraint(TypeVariable flex, IType rigid, FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            var f1 = rigid.MakeHull(fresh, out var hull, out var constraints);
            var freshDependencies = constraints.Select(c => new TypeVariableIntro(c.name, Kind.Unit));
            var contextConstraints = constraints.Select(c => new UnitConstraint(new TypeVariable(c.name), c.replaced));
            return new InferenceState(f1, prefix.Add(new FlexRigidHullConstraint(this.Dependencies.AddRange(freshDependencies), flex, hull)).AddRange(contextConstraints).AddRange(suffix));
        }
    }

    public class FlexRigidHullConstraint : IConstraint
    {
        public IImmutableList<IContextEntry> Dependencies { get; }
        public TypeVariable Flex { get; }
        public IType Rigid { get; }

        public FlexRigidHullConstraint(TypeVariable flex, IType rigid)
        {
            this.Dependencies = ImmutableList<IContextEntry>.Empty;
            this.Flex = flex;
            this.Rigid = rigid;
        }

        public FlexRigidHullConstraint(IImmutableList<IContextEntry> dependencies, TypeVariable flex, IType rigid)
        {
            this.Dependencies = dependencies;
            this.Flex = flex;
            this.Rigid = rigid;
        }
        
        public InferenceState Solve(FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            var top = prefix[prefix.Count - 1];
            var popped = prefix.RemoveAt(prefix.Count - 1);
            switch (top)
            {
                case LocalityMarker _: return new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix));
                case TermVariableBinding _: return new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix));
                case TypeVariableDefinition d:
                    return new InferenceState(fresh, popped.AddRange(this.Dependencies).Add(this.Substitute(d.Name, d.Definition)).Add(top).AddRange(suffix));
                case TypeVariableIntro i:
                    switch (i.Name == this.Flex.Name, this.Rigid.FreeVariables().Contains(i.Name))
                    {
                        case (true, true): throw new Exception("Occurs check failed!");
                        case (true, false): return new InferenceState(fresh, popped.AddRange(this.Dependencies).Add(new TypeVariableDefinition(i.Name, this.Rigid, Kind.Value)).AddRange(suffix));
                        case (false, true): return new InferenceState(fresh, popped.Add(new FlexRigidHullConstraint(this.Dependencies.Insert(0, i), this.Flex, this.Rigid)).AddRange(suffix));
                        case (false, false): return new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix));
                    }
                default: throw new Exception($"Unknown context entry: {top}");
            }
        }

        public IConstraint Substitute(string name, IType subWith) =>
            this.Rigid.FreeVariables().Union(new[] { this.Flex.Name }).Contains(name)
            ? new TypeConstraint(this.Flex.Substitute(name, subWith), this.Rigid.Substitute(name, subWith)) as IConstraint
            : new FlexRigidHullConstraint(this.Flex, this.Rigid);

        public override string ToString() => $"[{string.Join(", ", this.Dependencies.Select(c => c.ToString()))} | {this.Flex} == {this.Rigid}]";
    }

    public class UnitConstraint : IConstraint
    {
        public string? Dependency { get; }
        public Unit Equation { get; }

        public UnitConstraint(string? dependency, Unit equation)
        {
            this.Dependency = dependency;
            this.Equation = equation;
        }

        public UnitConstraint(IType left, IType right)
        {
            this.Dependency = null;
            this.Equation = left.ToUnit().Subtract(right.ToUnit());
        }

        public UnitConstraint(Unit equation)
        {
            this.Dependency = null;
            this.Equation = equation;
        }

        public UnitConstraint Substitute(string name, Unit subWith) =>
            new UnitConstraint(this.Dependency, this.Equation.Substitute(name, subWith));

        public override string ToString() => $"[{this.Dependency} | 1 == {this.Equation}]";

        public InferenceState Solve(FreshVariableStream fresh, IImmutableList<IContextEntry> prefix, IImmutableList<IContextEntry> suffix)
        {
            if (this.Equation.IsIdentity())
            {
                return new InferenceState(fresh, prefix.AddRange(suffix));
            }
            if (this.Equation.IsConstant())
            {
                throw new Exception("Unit mismatch!");
            }

            var top = prefix[prefix.Count - 1];
            var popped = prefix.RemoveAt(prefix.Count - 1);
            switch (top)
            {
                case LocalityMarker _:
                case TermVariableBinding _:
                    return new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix));
                case TypeVariableDefinition d:
                    return this.Equation.ExponentOf(d.Name) != 0
                        ? (this.Dependency != null
                            ? new InferenceState(fresh, popped.Add(new TypeVariableIntro(this.Dependency, Kind.Unit)).Add(this.Substitute(d.Name, d.Definition.ToUnit())).Add(d).AddRange(suffix))
                            : new InferenceState(fresh, popped.Add(this.Substitute(d.Name, d.Definition.ToUnit())).Add(d).AddRange(suffix)))
                        : new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix));
                case TypeVariableIntro i:
                    var variableScalar = this.Equation.ExponentOf(i.Name);
                    if (variableScalar == 0)
                    {
                        return new InferenceState(fresh, popped.Add(this).Add(top).AddRange(suffix));
                    }
                    else if (this.Equation.DividesPowers(variableScalar))
                    {
                        var pivoted = this.Equation.Pivot(i.Name);
                        return this.Dependency != null
                            ? new InferenceState(fresh, popped
                                .Add(new TypeVariableIntro(this.Dependency, Kind.Unit))
                                .Add(new TypeVariableDefinition(i.Name, pivoted.ToType(), Kind.Unit))
                                .AddRange(suffix))
                            : new InferenceState(fresh, popped
                                .Add(new TypeVariableDefinition(i.Name, pivoted.ToType(), Kind.Unit))
                                .AddRange(suffix));
                    }
                    else if (this.Equation.NotMax(i.Name))
                    {
                        var freshSeq = fresh.Next("b", out var freshVar);
                        var pivoted = this.Equation.Pivot(i.Name).Add(new Unit(freshVar));
                        return new InferenceState(freshSeq,
                            popped.Add(this.Substitute(i.Name, pivoted))
                                .Add(new TypeVariableDefinition(i.Name, pivoted.ToType(), Kind.Unit))
                                .AddRange(suffix));
                    }
                    else if (this.Equation.Variables.Count > 1)
                    {
                        return new InferenceState(fresh, popped.Add(new UnitConstraint(i.Name, this.Equation)).AddRange(suffix));
                    }
                    else
                    {
                        throw new Exception("Bad state encountered while performing unit unification.");
                    }
                default:
                    throw new Exception("Unknown entry type in unit unification context.");
            }
        }
    }
}
