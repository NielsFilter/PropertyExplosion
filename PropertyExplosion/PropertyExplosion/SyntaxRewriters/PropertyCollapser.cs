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
    public class PropertyCollapser : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly ISymbol _backingField;
        private readonly PropertyDeclarationSyntax _fullProperty;

        public PropertyCollapser(SemanticModel semanticModel, ISymbol backingField, PropertyDeclarationSyntax property)
        {
            this._semanticModel = semanticModel;
            this._backingField = backingField;
            this._fullProperty = property;
        }

        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax field)
        {
            // Retrieve the symbol for the field
            if (field.Declaration.Variables.Count == 1)
            {
                var variable = field.Declaration.Variables.First();
                if (object.Equals(_semanticModel.GetDeclaredSymbol(variable), this._backingField))
                {
                    return null;
                }
            }

            return base.VisitFieldDeclaration(field);    
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax propertyDeclaration)
        {
            if (propertyDeclaration == this._fullProperty)
            {
                // Produce the new property.
                var emptyGetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                var emptySetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

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
