using System.Collections.Concurrent;

namespace ProgramableNetwork
{
    /// <summary>
    /// Caches prefixed dictionary keys to avoid repeated string allocations in
    /// hot paths. Module data accessors (Input, Output, Field, Display) all
    /// build keys like "in__" + name on every read/write. Because the set of
    /// distinct <c>name</c> values is small and fixed per module prototype
    /// (they come from proto input/output/field IDs), caching them is safe and
    /// dramatically reduces GC pressure.
    ///
    /// Thread-safe via ConcurrentDictionary — these may be read from both the
    /// sim thread and the main thread (e.g., display data read during UI render).
    /// </summary>
    internal static class PrefixedKeyCache
    {
        // One cache per prefix. Each inner dictionary maps the bare name to
        // the concatenated "prefix + name" string. Using separate caches per
        // prefix keeps lookups fast and avoids compound-key allocations.
        private static readonly ConcurrentDictionary<string, string> s_inputKeys = new();
        private static readonly ConcurrentDictionary<string, string> s_outputKeys = new();
        private static readonly ConcurrentDictionary<string, string> s_fieldKeys = new();
        private static readonly ConcurrentDictionary<string, string> s_displayKeys = new();

        private const string InputPrefix = "in__";
        private const string OutputPrefix = "out__";
        private const string FieldPrefix = "field__";
        private const string DisplayPrefix = "display__";

        public static string InputKey(string name)
            => s_inputKeys.GetOrAdd(name, static n => InputPrefix + n);

        public static string OutputKey(string name)
            => s_outputKeys.GetOrAdd(name, static n => OutputPrefix + n);

        public static string FieldKey(string name)
            => s_fieldKeys.GetOrAdd(name, static n => FieldPrefix + n);

        public static string DisplayKey(string name)
            => s_displayKeys.GetOrAdd(name, static n => DisplayPrefix + n);
    }
}
