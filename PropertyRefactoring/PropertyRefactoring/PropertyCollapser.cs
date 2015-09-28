using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyRefactoring
{
    public class PropertyCollapser : CSharpSyntaxRewriter
    {
        private readonly SemanticModel semanticModel;
        private readonly ISymbol backingField;
        private readonly PropertyDeclarationSyntax property;

        public PropertyCollapser(SemanticModel semanticModel, ISymbol backingField, PropertyDeclarationSyntax property)
        {
            this.semanticModel = semanticModel;
            this.backingField = backingField;
            this.property = property;
        }

        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax field)
        {
            // Retrieve the symbol for the field
            if (field.Declaration.Variables.Count == 1)
            {
                var variable = field.Declaration.Variables.First();
                if (object.Equals(semanticModel.GetDeclaredSymbol(variable), backingField))
                {
                    return null;
                }
            }

            return base.VisitFieldDeclaration(field);    
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax propertyDeclaration)
        {
            if (propertyDeclaration == property)
            {
                // Produce the new property.
                var emptyGetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                var emptySetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

                // Create a new auto property (without a body)
                var newProperty = property.WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new[] { emptyGetter, emptySetter })));

                return newProperty;
            }

            return base.VisitPropertyDeclaration(propertyDeclaration);
        }
    }
}
