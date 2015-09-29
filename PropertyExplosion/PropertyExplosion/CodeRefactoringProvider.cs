using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using PropertyExplosion.SyntaxRewriters;
using System;

namespace PropertyExplosion
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(PropertyExplosionCodeRefactoringProvider)), Shared]
    internal class PropertyExplosionCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            // Find the Property at the selection
            var propertyDeclaration = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            if (propertyDeclaration == null)
            {
                return; // No property here... move along!
            }

            if (isAutoProperty(propertyDeclaration))
            {
                // Auto Property is selected (Get ready for EXPLOSION)
                var explodeAction = CodeAction.Create("Explode Property", c => ExplodePropertyAsync(root, context.Document, propertyDeclaration, c));
                context.RegisterRefactoring(explodeAction);
            }
            else
            {
                // Full Property is selected (Get ready to CRUCH)
                var implodeAction = CodeAction.Create("Crunch Property", c => ImplodePropertyAsync(root, context.Document, propertyDeclaration, c));
                context.RegisterRefactoring(implodeAction);
            }
        }

        private static bool isAutoProperty(PropertyDeclarationSyntax property)
        {
            var accessors = property.AccessorList.Accessors;

            var getter = accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);
            var setter = accessors.FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);

            if (getter != null && setter != null)
            {
                // Check if getter and setter are both empty (i.e. an auto-property).
                return getter.Body == null && setter.Body == null;
            }

            return false;
        }

        #region Explode Property

        private async Task<Document> ExplodePropertyAsync(SyntaxNode oldRoot, Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);

            // Call the Property Exploder. The code refactoring happens here.
            var expander = new PropertyExploder(property.Parent, property);
            var newRoot = expander.Visit(oldRoot);

            // Update code with the refactored Root
            return document.WithSyntaxRoot(newRoot);
        }

        #endregion

        #region Implode Property

        private async Task<Document> ImplodePropertyAsync(SyntaxNode oldRoot, Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Get the property's backing field.
            var backingField = this.GetBackingField(semanticModel, property);
            if (backingField == null)
            {
                return document; // No need to continue...
            }

            // Call the Property Collapser. The code refactoring happens here.
            var collapser = new PropertyCollapser(semanticModel, backingField, property);
            var newRoot = collapser.Visit(oldRoot);

            // Update code with the refactored Root
            return document.WithSyntaxRoot(newRoot);
        }

        private IFieldSymbol GetBackingField(SemanticModel semanticModel, PropertyDeclarationSyntax property)
        {
            var statements = property.AccessorList.Accessors.FirstOrDefault(ad => ad.Kind() == SyntaxKind.GetAccessorDeclaration).Body.Statements;
            if (statements.Count == 1)
            {
                var returnStatement = statements.FirstOrDefault() as ReturnStatementSyntax;
                if (returnStatement != null && returnStatement.Expression != null)
                {
                    var parentType = semanticModel.GetDeclaredSymbol(property).ContainingType;
                    var fieldSymbol = semanticModel.GetSymbolInfo(returnStatement.Expression).Symbol as IFieldSymbol;

                    if (fieldSymbol != null && parentType == fieldSymbol.OriginalDefinition.ContainingType)
                    {
                        return fieldSymbol;
                    }
                }
            }

            return null;
        }

        #endregion
    }
}