// Global using directives

global using System.Collections.Immutable;
global using System.Text;
global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
global using Microsoft.CodeAnalysis.CSharp.Syntax;
global using Microsoft.CodeAnalysis.Diagnostics;
global using Microsoft.CodeAnalysis.Text;

// Support for record types in netstandard2.0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
