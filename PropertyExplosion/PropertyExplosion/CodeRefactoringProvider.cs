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
        //*** This is the entry point when user hits Ctrl + . ***
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            
            // Find the Property at the selection
            var node = root.FindNode(context.Span);
            var property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();

            if (property == null)
            {
                return; // No property here... move along!
            }

            // Check if user is on an Auto property
            if (property.IsAutoProperty() || property.IsExpressionProperty())
            {
                // Auto Property is selected (Get ready for EXPLOSION)
                var explodeAction = CodeAction.Create("Explode Property...", c => ExplodePropertyAsync(root, context.Document, property, c));
                context.RegisterRefactoring(explodeAction); // Register Explode Code Action (This will show Explode Property... in the context menu)
            }
            else if (property.HasGetter())
            {
                // Full Property is selected (Get ready to CRUNCH)
                var implodeAction = CodeAction.Create("Crunch Property...", c => CrunchPropertyAsync(root, context.Document, property, c));
                context.RegisterRefactoring(implodeAction); // Register Crunch Code Action (This will show Crunch Property... in the context menu)
            }
        }

        #region Explode - Code Action

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

        #region Crunch - Code Action

        private async Task<Document> CrunchPropertyAsync(SyntaxNode oldRoot, Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Get the property's backing field.
            var backingField = property.GetBackingField(semanticModel);

            // Call the Property Cruncher. The code refactoring happens here.
            var collapser = new PropertyCruncher(semanticModel, backingField, property);
            var newRoot = collapser.Visit(oldRoot);

            // Update code with the refactored Root
            return document.WithSyntaxRoot(newRoot);
        }

        #endregion
    }
}