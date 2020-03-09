using System.Collections.Immutable;

namespace Inference.Common
{
    public class FreshVariableStream
    {
        private readonly IImmutableDictionary<string, uint> _freshByPrefix;

        public FreshVariableStream()
        {
            this._freshByPrefix = ImmutableDictionary<string, uint>.Empty;
        }

        private FreshVariableStream(IImmutableDictionary<string, uint> current)
        {
            this._freshByPrefix = current;
        }

        public FreshVariableStream Next(string prefix, out string fresh)
        {
            if (this._freshByPrefix.ContainsKey(prefix))
            {
                var suffix = this._freshByPrefix[prefix];
                fresh = $"{prefix}{suffix}";
                return new FreshVariableStream(this._freshByPrefix.SetItem(prefix, suffix + 1));
            }
            else
            {
                fresh = $"{prefix}0";
                return new FreshVariableStream(this._freshByPrefix.Add(prefix, 1));
            }
        }
    }
}
