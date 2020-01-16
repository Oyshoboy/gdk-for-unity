using System;

namespace Improbable.Gdk.CodeGeneration.CodeWriter.Scopes
{
    /// <summary>
    /// A generic scope with optional declaration.
    /// </summary>
    public class CustomScopeBlock : ScopeBody
    {
        internal CustomScopeBlock(Action<CustomScopeBlock> populate) : base(string.Empty)
        {
            populate(this);
        }

        internal CustomScopeBlock(string declaration, Action<CustomScopeBlock> populate) : base(declaration)
        {
            populate(this);
        }
    }
}