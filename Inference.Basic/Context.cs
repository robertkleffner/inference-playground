using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Inference.Basic
{
    public interface IContextEntry
    {
        IType Apply(IType subbed) => subbed;
    }

    public static class ContextListExt
    {
        public static IType Apply(this IReadOnlyList<IContextEntry> context, IType subbed) =>
            context.Aggregate(subbed, (type, entry) => entry.Apply(type));

        public static IImmutableList<IContextEntry> Normalize(this IReadOnlyList<IContextEntry> context) =>
            context.Aggregate(ImmutableList.Create<IContextEntry>(), (normalized, entry) =>
                entry is TypeVariableDefinition t
                    ? normalized.Add(new TypeVariableDefinition(t.Name, normalized.Apply(t.Definition)))
                    : normalized.Add(entry));
    }

    public class TypeVariableIntro : IContextEntry
    {
        public string Name { get; }

        public TypeVariableIntro(string name) { this.Name = name; }

        public override string ToString() => $"{this.Name} : *";
    }

    public class TypeVariableDefinition : IContextEntry
    {
        public string Name { get; }
        public IType Definition { get; }

        public TypeVariableDefinition(string name, IType definition) { this.Name = name; this.Definition = definition; }

        public override string ToString() => $"{this.Name} = {this.Definition} : *";

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
}
