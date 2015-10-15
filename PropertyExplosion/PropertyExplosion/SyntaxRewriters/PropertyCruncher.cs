using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyExplosion.SyntaxRewriters
{
    /// <summary>
    /// Syntax Rewriter to CRUNCH properties from Full into Auto-Properties
    /// </summary>
    public class PropertyCruncher : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly ISymbol _backingField;
        private readonly PropertyDeclarationSyntax _fullProperty;

        #region Constructors

        public PropertyCruncher(SemanticModel semanticModel, ISymbol backingField, PropertyDeclarationSyntax property)
        {
            this._semanticModel = semanticModel;
            this._backingField = backingField;
            this._fullProperty = property;
        }

        #endregion

        public override SyntaxNode VisitEmptyStatement(EmptyStatementSyntax node)
        {
            // Without this, the regions and comments get gobbled up when returning null for a SyntaxNode
            // Turns out regions are comments are not nodes but trivia of the node, so when removing node, the triva also gets cleared.
            return node.WithSemicolonToken(
                    SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken)
                        .WithLeadingTrivia(node.SemicolonToken.LeadingTrivia)
                        .WithTrailingTrivia(node.SemicolonToken.TrailingTrivia));
        }

        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax field)
        {
            if (this._backingField != null)
            {
                // Retrieve the symbol for the field
                if (field.Declaration.Variables.Count == 1)
                {
                    var variable = field.Declaration.Variables.First();
                    if (object.Equals(_semanticModel.GetDeclaredSymbol(variable), this._backingField))
                    {
                        // We've found the backing field of the property. We don't need it anymore, so return null (means original is "replaced" by nothing)
                        return null;
                    }
                }
            }

            return base.VisitFieldDeclaration(field);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax propertyDeclaration)
        {
            // Check each node until we find the property in question. When we have it, we'll replace it with an Auto Property
            if (propertyDeclaration == this._fullProperty)
            {
                // Create Empty getters and setters.
                var emptyGetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                var emptySetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                if (propertyDeclaration.HasGetter())
                {
                    // Put the original Get modifier on the Auto property
                    emptyGetter = emptyGetter.WithModifiers(propertyDeclaration.GetGetter().Modifiers);
                }
                else
                {
                    // Full property didn't have a getter, but no get in a Auto property makes no sense... We'll keep a get, but make it private
                    emptyGetter = emptyGetter.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)));
                }

                if (propertyDeclaration.HasSetter())
                {
                    // Put the original Set modifier on the Auto property
                    emptySetter = emptySetter.WithModifiers(propertyDeclaration.GetSetter().Modifiers);
                }
                else
                {
                    // Full property didn't have a setter, but no set in an Auto property makes no sense... We'll keep a set, but make it private
                    emptySetter = emptySetter.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)));
                }

                // Create a new auto property (without a body)
                var newProperty = _fullProperty.WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new[] { emptyGetter, emptySetter })));

                return newProperty;
            }

            return base.VisitPropertyDeclaration(propertyDeclaration);
        }
    }
}
