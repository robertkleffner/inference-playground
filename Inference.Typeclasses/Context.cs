using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Inference.Common;

namespace Inference.Typeclasses
{
    public interface IContextEntry
    {
        IType Apply(IType subbed) => subbed;
    }

    public static class ContextListExt
    {
        public static IType Apply(this IReadOnlyList<IContextEntry> context, IType subbed) =>
            context.Aggregate(subbed, (type, entry) => entry.Apply(type));

        public static Predicate Apply(this IReadOnlyList<IContextEntry> context, Predicate subbed) =>
            context.Aggregate(subbed, (pred, entry) => new Predicate(pred.Name, entry.Apply(pred.Arg)));

        public static QualifiedType Apply(this IReadOnlyList<IContextEntry> context, QualifiedType subbed) =>
            new QualifiedType(subbed.Context.Select(p => context.Apply(p)).ToImmutableList(), context.Apply(subbed.Head));

        public static IImmutableList<IContextEntry> Normalize(this IReadOnlyList<IContextEntry> context) =>
            context.Aggregate(ImmutableList.Create<IContextEntry>(), (normalized, entry) =>
                entry is TypeVariableDefinition t
                    ? normalized.Add(new TypeVariableDefinition(t.Name, normalized.Apply(t.Definition)))
                    : normalized.Add(entry));

        public static TypeclassDeclaration? FindClass(this IReadOnlyList<IContextEntry> context, string name) =>
            context.First(e => e is TypeclassDeclaration d && d.Name == name) as TypeclassDeclaration;

        public static IImmutableList<IContextEntry> ModifyClass(this IImmutableList<IContextEntry> context, TypeclassDeclaration declaration)
        {
            var classAndIndex = context.Select((c, i) => (c, i)).Where(p => p.c is TypeclassDeclaration d && d.Name == declaration.Name).First();
            if (classAndIndex.c is TypeclassDeclaration d)
            {
                return context.SetItem(classAndIndex.i, declaration);
            }
            throw new Exception($"Typeclass '{declaration.Name}' could not be found in the context.");
        }

        public static IImmutableList<IContextEntry> AddClass(this IImmutableList<IContextEntry> environment, string className) =>
            environment.Any(c => c is TypeclassDeclaration d && d.Name == className)
            ? throw new Exception($"Typeclass '{className}' is already defined.")
            : environment.Add(new TypeclassDeclaration(className));

        public static IImmutableList<IContextEntry> AddInstance(this IImmutableList<IContextEntry> environment, string className, TypeScheme inst)
        {
            var tclass = environment.FindClass(className);
            if (tclass == null)
            {
                throw new Exception($"Couldn't add instance {inst} for class '{className}' because it the class does not exist.");
            }

            // test for overlap by instantiating every existing instance and the new one (need instantiated in case the body of the qualified types have intersecting variable names)
            var (instContext, freshStream) = (ImmutableList<IContextEntry>.Empty as IImmutableList<IContextEntry>, new FreshVariableStream());
            var specializedInstances = tclass.Instances.Select(i => i.Instantiate(ref instContext, ref freshStream));
            var specializedAdd = inst.Instantiate(ref instContext, ref freshStream);
            var overlappingInstances = specializedInstances.Where(c => c.Head.Overlap(specializedAdd.Head));
            if (overlappingInstances.Any())
            {
                throw new Exception($"Instances {inst} overlaps with {string.Join(",", overlappingInstances.Select(i => $"({i.ToString()})"))} in class '{className}'");
            }

            return environment.ModifyClass(new TypeclassDeclaration(tclass.Name, tclass.Instances.Add(inst)));
        }

        public static IImmutableList<Predicate>? GetInstanceSubgoals(this IImmutableList<IContextEntry> context, Predicate instanceHead)
        {
            var typeclass = context.FindClass(instanceHead.Name);
            if (typeclass != null)
            {
                var matchingInstances = typeclass.Instances
                    .Select(inst => (inst, sub: inst.Body.Head.Match(instanceHead.Arg)))
                    .Where(match => match.sub != null);
                if (matchingInstances.Any())
                {
                    var (inst, sub) = matchingInstances.First();
                    return sub?.Apply(inst.Body.Context);
                }
                return null;
            }
            throw new Exception($"Encountered a predicated {instanceHead} with no associated typeclass.");
        }

        public static bool Entails(this IImmutableList<IContextEntry> context, IImmutableList<Predicate> conditions, Predicate test)
        {
            var subgoals = context.GetInstanceSubgoals(test);
            return subgoals.All(sub => context.Entails(conditions, sub));
        }
    }

    public class TypeVariableIntro : IContextEntry
    {
        public string Name { get; }
        public IKind Kind { get; }

        public TypeVariableIntro(string name, IKind kind)
        {
            this.Name = name;
            this.Kind = kind;
        }

        public override string ToString() => $"{this.Name} : {this.Kind}";
    }

    public class TypeVariableDefinition : IContextEntry
    {
        public string Name { get; }
        public IType Definition { get; }

        public TypeVariableDefinition(string name, IType definition) { this.Name = name; this.Definition = definition;; }

        public override string ToString() => $"{this.Name} = {this.Definition} : {this.Definition.Kind}";

        public IType Apply(IType subbed) => subbed.Substitute(this.Name, this.Definition);
    }

    public class TermVariableBinding : IContextEntry
    {
        public string Name { get; }
        public TypeScheme Type { get; }

        public TermVariableBinding(string name, TypeScheme type) { this.Name = name; this.Type = type; }

        public override string ToString() => $"{this.Name} : {this.Type}";
    }

    public class LocalityMarker : IContextEntry
    {
        public override string ToString() => ";;";
    }

    public class TypeclassDeclaration : IContextEntry
    {
        public string Name { get; }
        public IImmutableList<TypeScheme> Instances { get; }

        public TypeclassDeclaration(string name)
        {
            this.Name = name;
            this.Instances = ImmutableList<TypeScheme>.Empty;
        }

        public TypeclassDeclaration(string name, IImmutableList<TypeScheme> instances)
        {
            this.Name = name;
            this.Instances = instances;
        }
    }
}
