using System;
using System.Collections.Immutable;

namespace Inference.Common
{
    public interface IHasVariables<N> where N : IEquatable<N>
    {
        IImmutableSet<N> FreeVariables();
    }

    public interface ICanSubstitute<N, T> : IHasVariables<N> where N : IEquatable<N>
    {
        T Substitute(N replace, T replaceWith);
    }
}
