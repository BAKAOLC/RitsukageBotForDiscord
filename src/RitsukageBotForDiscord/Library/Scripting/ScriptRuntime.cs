using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace RitsukageBot.Library.Scripting
{
    internal class ScriptRuntime
    {
        public static readonly ScriptOptions DefaultOptions = ScriptOptions.Default
            .WithFileEncoding(Encoding.UTF8)
            .WithReferences(AppDomain.CurrentDomain.GetAssemblies())
            .WithImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Threading",
                "System.Threading.Tasks"
            )
            .WithLanguageVersion(LanguageVersion.Latest);

        public readonly Script Script;

        private ScriptRuntime(string script, ScriptOptions options)
        {
            Script = CSharpScript.Create(script, options);
        }

        private ScriptRuntime(Stream stream, ScriptOptions options)
        {
            Script = CSharpScript.Create(stream, options);
        }

        private ScriptRuntime(byte[] bytes, ScriptOptions options)
        {
            Script = CSharpScript.Create(new MemoryStream(bytes), options);
        }

        private ScriptRuntime(string script)
        {
            Script = CSharpScript.Create(script, DefaultOptions);
        }

        private ScriptRuntime(Stream stream)
        {
            Script = CSharpScript.Create(stream, DefaultOptions);
        }

        private ScriptRuntime(byte[] bytes)
        {
            Script = CSharpScript.Create(new MemoryStream(bytes), DefaultOptions);
        }

        public string Code => Script.Code;

        public Type GlobalsType => Script.GlobalsType;

        public Type ReturnType => Script.ReturnType;

        public Compilation Compilation => Script.GetCompilation();

        public ImmutableArray<Diagnostic> Compile()
        {
            return Script.Compile();
        }

        public (AssemblyInfo, ImmutableArray<Diagnostic>) CompileToAssembly(string name)
        {
            var ms = new MemoryStream();
            var il = Compilation.Emit(ms);
            if (!il.Success)
                throw new CompilationErrorException("Compilation failed", il.Diagnostics);
            ms.Seek(0, SeekOrigin.Begin);
            return (new(Assembly.Load(ms.ToArray()), name), il.Diagnostics);
        }

        public Task<ScriptState> RunAsync()
        {
            return Script.RunAsync();
        }

        public static ScriptRuntime Create(string script, ScriptOptions? options = null)
        {
            if (options is not null)
                return new(script, options);
            return new(script);
        }

        public static ScriptRuntime Create(Stream stream, ScriptOptions? options = null)
        {
            if (options is not null)
                return new(stream, options);
            return new(stream);
        }

        public static ScriptRuntime Create(byte[] bytes, ScriptOptions? options = null)
        {
            if (options is not null)
                return new(bytes, options);
            return new(bytes);
        }

        internal readonly struct AssemblyInfo(Assembly assembly, string name)
        {
            public Assembly Assembly { get; } = assembly;

            public string Name { get; } = name;
        }
    }
}