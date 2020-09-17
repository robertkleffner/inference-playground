using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Inference.Common;

/*
 * [(a,b)...] ~ [(I, c) d (e, f)...]
 * a -> I, a1, e ...
 * b -> c, b1, f ...
 * d -> (a1, b1)
 */

namespace Inference.Dots
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
        public static TypeConstructor TupleCons => new TypeConstructor("Tuple", new ArrowKind(PredefinedKinds.OnlySequenceKind, new DataKind()));

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

        public static IType Tuple(params IType[] elems) =>
            new TypeApplication(TupleCons, new TypeSequence(elems.ToImmutableList()));
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
            (this.Func.Substitute(replace, replaceWith), this.Arg.Substitute(replace, replaceWith)) switch
            {
                (TypeSequence ls, TypeSequence rs) => ls.Types.Count != rs.Types.Count
                    ? throw new Exception($"Substituting sequences of different lengths: {ls.Types} VS {rs.Types}")
                    : new TypeSequence(ls.Types.Zip(rs.Types, (l, r) => new TypeApplication(l, r)).ToImmutableList<IType>()),
                (TypeSequence ls, IType r) => new TypeSequence(ls.Types.Select(l => new TypeApplication(l, r)).ToImmutableList<IType>()),
                (IType l, TypeSequence rs) => new TypeSequence(rs.Types.Select(r => new TypeApplication(l, r)).ToImmutableList<IType>()),
                (IType l, IType r) => new TypeApplication(l, r)
            };

        public IImmutableSet<string> FreeVariables() =>
            this.Func.FreeVariables().Union(this.Arg.FreeVariables());

        public override string ToString() => $"({this.Func} {this.Arg})";

        public override bool Equals(object obj) =>
            obj is TypeApplication other
            ? other.Func.Equals(this.Func) && other.Arg.Equals(this.Arg)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash(this.Func).Hash(this.Arg);

        public void Deconstruct(out IType left, out IType right)
        {
            left = this.Func;
            right = this.Arg;
        }
    }

    public class TypeSequence : IType
    {
        public IImmutableList<IType> Types { get; }
        public IType? Dotted { get; }

        public IKind Kind => PredefinedKinds.OnlySequenceKind;

        public bool IsHeadNormalForm => false;

        public TypeSequence(IImmutableList<IType> types)
        {
            this.Types = types;
            this.Dotted = null;
        }

        public TypeSequence(IImmutableList<IType> types, IType dotted)
        {
            this.Types = types;
            this.Dotted = dotted;
        }

        public IImmutableDictionary<string, IType>? Match(IType compared)
        {
            if (compared is TypeSequence other)
            {
                if (other.Types.Count < this.Types.Count)
                {
                    return null;
                }
                if (other.Dotted != null && this.Dotted == null)
                {
                    return null;
                }

                IImmutableDictionary<string, IType>? matched = ImmutableDictionary<string, IType>.Empty;
                for (int i = 0; i < this.Types.Count; i++)
                {
                    if (matched == null) { break; }
                    var submatch = this.Types[i].Match(other.Types[i]);
                    matched = submatch != null ? matched.Merge(submatch) : null;
                }

                if (this.Dotted != null)
                {
                    var dottedFree = this.Dotted.FreeVariables();
                    IImmutableDictionary<string, IType>? dotmatched = ImmutableDictionary<string, IType>.Empty;
                    for (int i = this.Types.Count; i < other.Types.Count; i++)
                    {
                        if (dotmatched == null) { break; }
                        var submatch = this.Dotted.Match(other.Types[i]);
                        dotmatched = submatch != null ? dotmatched.MergeWithSeqVars(submatch, dottedFree) : null;
                    }

                    if (dotmatched != null && other.Dotted != null)
                    {
                        var submatch = this.Dotted.Match(other.Dotted);
                        dotmatched = submatch != null ? dotmatched.CapWithDotted(submatch) : null;
                    }
                    return dotmatched;
                }
                else
                {
                    return matched;
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

        public IType Substitute(string replace, IType replaceWith)
        {
            var firstSeqSub = this.Types
                .Select(t => t.Substitute(replace, replaceWith))
                .Select(t => t switch
                {
                    TypeSequence ts => ts.Types,
                    _ => ImmutableList.Create(t)
                })
                .Aggregate((ls, rs) => ls.AddRange(rs));

            if (this.Dotted != null)
            {
                return this.Dotted.Substitute(replace, replaceWith) switch
                {
                    TypeSequence ts => ts.Dotted != null
                        ? new TypeSequence(firstSeqSub.AddRange(ts.Types), ts.Dotted)
                        : new TypeSequence(firstSeqSub.AddRange(ts.Types)),
                    IType t => new TypeSequence(firstSeqSub, t)
                };
            }
            else
            {
                return new TypeSequence(firstSeqSub);
            }
        }

        public IImmutableSet<string> FreeVariables() =>
            this.Types.Aggregate(ImmutableHashSet<string>.Empty, (agg, t) => agg.Union(t.FreeVariables()));

        public override string ToString() => $"[{string.Join(", ", this.Types.Select(t => t.ToString()))}]";

        public override bool Equals(object obj) =>
            obj is TypeSequence other
            ? other.Types.Zip(this.Types, (t1, t2) => t1.Equals(t2)).All(t => t)
            : false;

        public override int GetHashCode() => Hashing.Start.Hash("[]").Hash(this.Types);
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

        public IImmutableList<Predicate> Substitute(string replace, IType replaceWith) =>
            this.Arg.Substitute(replace, replaceWith) switch
            {
                TypeSequence ts => ts.Types.Select(t => new Predicate(this.Name, t)).ToImmutableList(),
                IType t => ImmutableList.Create(new Predicate(this.Name, t))
            };

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
        public static IEnumerable<string> FreeVariables(this IEnumerable<Predicate> predicates) =>
            predicates.SelectMany(pred => pred.FreeVariables()).Distinct();

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

        public QualifiedType(IType head, params Predicate[] context)
        {
            this.Context = context.ToImmutableList();
            this.Head = head;
        }

        public QualifiedType Substitute(string replace, IType replaceWith) =>
            new QualifiedType(
                this.Context.SelectMany(p => p.Substitute(replace, replaceWith)).ToImmutableList(),
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
