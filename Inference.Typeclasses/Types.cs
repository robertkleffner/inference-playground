using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Typeclasses
{
    public interface IType : IHasVariables<string>, ICanSubstitute<string, IType>
    {
        IKind Kind { get; }
        bool IsHeadNormalForm { get; }

        IImmutableDictionary<string, IType>? Match(IType compared);
        IImmutableDictionary<string, IType> Unify(IType compared);

        public bool Overlap(IType compared)
        {
            try
            {
                var result = this.Unify(compared);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static class PrimType
    {
        public static TypeConstructor FunCons => new TypeConstructor("->", new ArrowKind(new DataKind(), new ArrowKind(new DataKind(), new DataKind())));

        public static IType Fun(IType input, IType output, params IType[] moreOutput)
        {
            if (moreOutput.Any())
            {
                return new TypeApplication(new TypeApplication(FunCons, input), Fun(output, moreOutput.First(), moreOutput.Skip(1).ToArray()));
            }
            else
            {
                return new TypeApplication(new TypeApplication(FunCons, input), output);
            }
        }
    }

    public class TypeVariable : IType
    {
        public string Name { get; }

        public IKind Kind { get; }

        public bool IsHeadNormalForm => true;

        public TypeVariable(string name, IKind kind) { this.Name = name; this.Kind = kind; }

        public IImmutableSet<string> FreeVariables() =>
            ImmutableHashSet.Create(this.Name);

        public IType Substitute(string name, IType subWith) =>
            this.Name == name ? subWith : this;

        public IImmutableDictionary<string, IType>? Match(IType compared) =>
            this.Kind.Equals(compared.Kind)
                ? ImmutableDictionary.Create<string, IType>().Add(this.Name, compared)
                : null;

        public IImmutableDictionary<string, IType> Unify(IType compared)
        {
            if (this.Kind.Equals(compared.Kind))
            {
                if (this.Equals(compared))
                {
                    return ImmutableDictionary.Create<string, IType>();
                }
                else if (compared.FreeVariables().Contains(this.Name))
                {
                    throw new Exception($"Occurs check failed on '{this}' in {compared}");
                }
                else
                {
                    return ImmutableDictionary.Create<string, IType>().Add(this.Name, compared);
                }
            }
            else
            {
                throw new Exception($"Variable '{this}' did not match the kind of {compared} during unification.");
            }
        }

        public override string ToString() => this.Name;

        public override bool Equals(object obj) => obj is TypeVariable other ? other.Name.Equals(this.Name) : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Name);
    }

    public class TypeConstructor : IType
    {
        public string Name { get; }

        public IKind Kind { get; }

        public bool IsHeadNormalForm => false;

        public TypeConstructor(string name, IKind kind) { this.Name = name; this.Kind = kind; }

        public IImmutableSet<string> FreeVariables() => ImmutableHashSet<string>.Empty;

        public IType Substitute(string replace, IType replaceWith) => this;

        public IImmutableDictionary<string, IType>? Match(IType compared) =>
            (compared is TypeConstructor other && this.Name == other.Name)
            ? ImmutableDictionary.Create<string, IType>()
            : null;

        public IImmutableDictionary<string, IType> Unify(IType compared)
        {
            if (compared is TypeVariable other)
            {
                return other.Unify(this);
            }
            else if (this.Equals(compared))
            {
                return ImmutableDictionary.Create<string, IType>();
            }
            throw new Exception($"Could not unify {this} with {compared}");
        }

        public override string ToString() => this.Name;

        public override bool Equals(object obj) => obj is TypeConstructor other ? other.Name.Equals(this.Name) : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Name);
    }

    public class TypeApplication : IType
    {
        public IType Func { get; }
        public IType Arg { get; }

        public bool IsHeadNormalForm => this.Func.IsHeadNormalForm;

        public IKind Kind
        {
            get
            {
                var funKind = this.Func.Kind;
                var argKind = this.Arg.Kind;
                if (funKind is ArrowKind arrow)
                {
                    if (arrow.Func.Equals(argKind))
                    {
                        return arrow.Arg;
                    }
                    throw new Exception($"Kind error: tried to apply {funKind} to {argKind} in ({this.Func} {this.Arg})");
                }
                throw new Exception($"Kind error: type expression ({this.Func} {this.Arg}) is an application but the applied part does not have an arrow kind.");
            }
        }

        public TypeApplication(IType func, IType arg, params IType[] rest)
        {
            if (!rest.Any())
            {
                this.Func = func;
                this.Arg = arg;
            }
            else
            {
                this.Func = rest.SkipLast(1).Aggregate(new TypeApplication(func, arg), (app, nextArg) => new TypeApplication(app, nextArg));
                this.Arg = rest.Last();
            }
        }

        public IImmutableDictionary<string, IType>? Match(IType compared)
        {
            if (compared is TypeApplication other)
            {
                var left = this.Func.Match(other.Func);
                var right = this.Arg.Match(other.Arg);
                if (left != null && right != null)
                {
                    return left.Merge(right);
                }
            }
            return null;
        }

        public IImmutableDictionary<string, IType> Unify(IType compared)
        {
            if (compared is TypeVariable other)
            {
                return other.Unify(compared);
            }
            else if (compared is TypeApplication app)
            {
                var s1 = this.Func.Unify(app.Func);
                var s2 = s1.Apply(this.Arg).Unify(s1.Apply(app.Arg));
                return s2.Compose(s1);
            }
            throw new Exception($"Cloud not unify {this} with {compared}");
        }

        public IType Substitute(string replace, IType replaceWith) =>
            new TypeApplication(this.Func.Substitute(replace, replaceWith), this.Arg.Substitute(replace, replaceWith));

        public IImmutableSet<string> FreeVariables() =>
            this.Func.FreeVariables().Union(this.Arg.FreeVariables());

        public override string ToString() => $"({this.Func} {this.Arg})";

        public override bool Equals(object obj) =>
            obj is TypeApplication other
            ? other.Func.Equals(this.Func) && other.Arg.Equals(this.Arg)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Func).Hash(this.Arg);
    }

    public class Predicate : IHasVariables<string>
    {
        public string Name { get; }
        public IType Arg { get; }

        public IKind Kind => this.Arg.Kind;

        public bool IsHeadNormalForm => this.Arg.IsHeadNormalForm;

        public Predicate(string name, IType arg)
        {
            this.Name = name;
            this.Arg = arg;
        }

        public Predicate Substitute(string replace, IType replaceWith) =>
            new Predicate(this.Name, this.Arg.Substitute(replace, replaceWith));

        public IImmutableDictionary<string, IType>? Match(Predicate compared) =>
            (this.Name == compared.Name)
            ? this.Arg.Match(compared.Arg)
            : null;

        public IImmutableDictionary<string, IType> Unify(Predicate compared)
        {
            if (this.Name == compared.Name)
            {
                return this.Arg.Unify(compared.Arg);
            }
            throw new Exception($"Differing class constraints, {this.Name} vs {compared.Name}");
        }

        public IImmutableSet<string> FreeVariables() => this.Arg.FreeVariables();

        public IImmutableList<Predicate> ToHeadNormalForm(IImmutableList<IContextEntry> context)
        {
            if (this.IsHeadNormalForm)
            {
                return ImmutableList.Create(this);
            }
            else
            {
                var subgoals = context.GetInstanceSubgoals(this);
                if (subgoals != null)
                {
                    return subgoals.ToHeadNormalForm(context);
                }
                else
                {
                    throw new Exception($"Context reduction failed for {this}");
                }
            }
        }

        public override string ToString() => $"{this.Name} {this.Arg}";

        public override bool Equals(object obj) =>
            obj is Predicate other
            ? other.Name.Equals(this.Name) && other.Arg.Equals(this.Arg)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Name).Hash(this.Arg);
    }

    public static class PredicateExt
    {
        public static IImmutableList<Predicate> ToHeadNormalForm(this IImmutableList<Predicate> predicates, IImmutableList<IContextEntry> environment) =>
            predicates.SelectMany(p => p.ToHeadNormalForm(environment)).ToImmutableList();

        public static IImmutableList<Predicate> Simplify(this IImmutableList<Predicate> predicates, IImmutableList<IContextEntry> environment)
        {
            var simplified = ImmutableList<Predicate>.Empty;
            var remaining = predicates;
            while (remaining.Any())
            {
                var test = remaining.First();
                remaining = remaining.RemoveAt(0);
                if (!environment.Entails(remaining.AddRange(simplified), test))
                {
                    simplified = simplified.Add(test);
                }
            }
            return simplified;
        }

        public static IImmutableList<Predicate> Reduce(this IImmutableList<Predicate> predicates, IImmutableList<IContextEntry> environment)
        {
            var normalized = predicates.ToHeadNormalForm(environment);
            return normalized.Simplify(environment);
        }

        public static (IImmutableList<Predicate> deferred, IImmutableList<Predicate> generalized) SplitPredicates(this IImmutableList<Predicate> predicates,
            IImmutableList<IContextEntry> environment, IEnumerable<string> generalizedTypeVariables)
        {
            var reduced = predicates.Reduce(environment);
            var deferred = reduced.Where(p => p.FreeVariables().Except(generalizedTypeVariables).Any()).ToImmutableList();
            var generalized = reduced.Where(p => !p.FreeVariables().Except(generalizedTypeVariables).Any()).ToImmutableList();
            return (deferred, generalized);
        }
    }

    public class QualifiedType : IHasVariables<string>
    {
        public IImmutableList<Predicate> Context { get; }
        public IType Head { get; }

        public IKind Kind => this.Head.Kind;

        public QualifiedType(IType head)
        {
            this.Context = ImmutableList<Predicate>.Empty;
            this.Head = head;
        }

        public QualifiedType(IImmutableList<Predicate> context, IType head)
        {
            this.Context = context;
            this.Head = head;
        }

        public QualifiedType Substitute(string replace, IType replaceWith) =>
            new QualifiedType(
                this.Context.Select(p => p.Substitute(replace, replaceWith) as Predicate).ToImmutableList(),
                this.Head.Substitute(replace, replaceWith));

        public IImmutableSet<string> FreeVariables() =>
            this.Context
            .Select(p => p.Arg.FreeVariables())
            .Aggregate((s1, s2) => s1.Union(s2))
            .Union(this.Head.FreeVariables());

        public override bool Equals(object obj)
        {
            return obj is QualifiedType other
                ? this.Context.Count == other.Context.Count && this.Head.Equals(other.Head) && this.Context.Select((pred, ind) => pred.Equals(other.Context[ind])).All(x => x)
                : false;
        }

        public override int GetHashCode() =>
            Hashing.Start.Hash(this.Context).Hash(this.Head);

        public override string ToString() =>
            $"{string.Join(",", this.Context.Select(c => c.ToString()))} => {this.Head}";
    }

    public class TypeScheme : IHasVariables<string>
    {
        public IReadOnlyList<(string name, IKind kind)> Quantified { get; }
        public QualifiedType Body { get; }

        public TypeScheme(IType body, IReadOnlyList<(string, IKind)> quantified) { this.Body = new QualifiedType(body); this.Quantified = quantified; }
        public TypeScheme(QualifiedType body, IReadOnlyList<(string, IKind)> quantified) { this.Body = body; this.Quantified = quantified; }

        public TypeScheme(IType body, params (string, IKind)[] quantified) { this.Body = new QualifiedType(body); this.Quantified = quantified; }
        public TypeScheme(QualifiedType body, params (string, IKind)[] quantified) { this.Body = body; this.Quantified = quantified; }

        public IImmutableSet<string> FreeVariables() =>
            this.Body.FreeVariables()
                .Except(this.Quantified.Select(s => s.name));

        public QualifiedType Instantiate(ref IImmutableList<IContextEntry> context, ref FreshVariableStream stream)
        {
            var state = stream;
            var freshType = this.Body;
            foreach (var (name, kind) in this.Quantified)
            {
                state = state.Next("tc", out var tname);
                context = context.Add(new TypeVariableIntro(tname, kind));
                freshType = freshType.Substitute(name, new TypeVariable(tname, kind));
            }
            return freshType;
        }

        public override string ToString() => $"forall {string.Join(",", this.Quantified)}.{this.Body}";
    }
}
