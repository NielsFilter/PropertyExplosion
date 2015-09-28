using System;
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

namespace PropertyRefactoring
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(PropertyRefactoringCodeRefactoringProvider)), Shared]
    internal class PropertyRefactoringCodeRefactoringProvider : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindToken(context.Span.Start);

            var propertyDeclaration = node.Parent.FirstAncestorOrSelf<PropertyDeclarationSyntax>();

            if (propertyDeclaration == null)
            {
                return; // No property here... move along!
            }

            if (IsAutoProperty(propertyDeclaration))
            {
                var explodeAction = CodeAction.Create("Explode Property", c => ExplodePropertyAsync(context.Document, propertyDeclaration, c));
                context.RegisterRefactoring(explodeAction);
            }
            else
            {
                var implodeAction = CodeAction.Create("Implode Property", c => ImplodePropertyAsync(context.Document, propertyDeclaration, c));
                context.RegisterRefactoring(implodeAction);
            }
        }

        /// <summary>
        /// Returns true if both get and set accessors exist on the given property; otherwise false.
        /// </summary>
        private static bool IsAutoProperty(BasePropertyDeclarationSyntax property)
        {
            var accessors = property.AccessorList.Accessors;
            var getter = accessors.FirstOrDefault(ad => ad.Kind() == SyntaxKind.GetAccessorDeclaration);
            var setter = accessors.FirstOrDefault(ad => ad.Kind() == SyntaxKind.SetAccessorDeclaration);

            if (getter != null && setter != null)
            {
                // The getter and setter should have a body.
                return getter.Body == null && setter.Body == null;
            }

            return false;
        }

        #region Explode Property

        private async Task<Document> ExplodePropertyAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
        {
            try
            {
                var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                // Call the Property Exploder. The code refactoring happens here.
                var expander = new PropertyExploder(semanticModel, property.Parent, property);
                var newRoot = expander.Visit(root);
                return document.WithSyntaxRoot(newRoot);
            }
            catch (Exception)
            {
                return document;
            }
        }

        #endregion

        #region Implode Property

        private async Task<Document> ImplodePropertyAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
        {
            try
            {
                // Rewrite property
                var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                // Get the property's backing field.
                var backingField = this.GetBackingField(semanticModel, property);
                if (backingField == null)
                {
                    return document; // No need to continue
                }

                // Call the Property Collapser. The code refactoring happens here.
                var collapser = new PropertyCollapser(semanticModel, backingField, property);
                var newRoot = collapser.Visit(root);
                return document.WithSyntaxRoot(newRoot);
            }
            catch (Exception)
            {
                return document;
            }
        }

        private ISymbol GetBackingField(SemanticModel semanticModel, PropertyDeclarationSyntax property)
        {
            var statements = property.AccessorList.Accessors.FirstOrDefault(ad => ad.Kind() == SyntaxKind.GetAccessorDeclaration).Body.Statements;
            if (statements.Count == 1)
            {
                var returnStatement = statements.FirstOrDefault() as ReturnStatementSyntax;
                if (returnStatement != null && returnStatement.Expression != null)
                {
                    var containingType = semanticModel.GetDeclaredSymbol(property).ContainingType;
                    var symbolInfo = semanticModel.GetSymbolInfo(returnStatement.Expression);
                    var fieldSymbol = symbolInfo.Symbol as IFieldSymbol;

                    if (fieldSymbol != null && Equals(fieldSymbol.OriginalDefinition.ContainingType, containingType))
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