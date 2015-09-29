using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace PropertyExplosion.SyntaxRewriters
{
    public class PropertyExploder : CSharpSyntaxRewriter
    {
        private readonly SyntaxNode _propertyParent;
        private readonly PropertyDeclarationSyntax _autoProperty;
                
        public PropertyExploder(SyntaxNode propertyParent, PropertyDeclarationSyntax autoProperty)
        {
            this._propertyParent = propertyParent;
            this._autoProperty = autoProperty;
        }

        public override SyntaxNode Visit(SyntaxNode node)
        {
            if (_propertyParent.IsEquivalentTo(node))
            {
                // Take the name of the property, make first letter lower case and prefix with _ e.g. "MyProperty" becomes "_myProperty".
                var privateFieldName = this.getPrivateFieldName();

                // Create the private "backing field" of the property
                var privateField = SyntaxFactory.FieldDeclaration(
                                        SyntaxFactory.VariableDeclaration(_autoProperty.Type, // Property Type
                                            SyntaxFactory.SeparatedList(
                                                new[] { SyntaxFactory.VariableDeclarator(
                                                    SyntaxFactory.Identifier(privateFieldName)) }))) // Field Name
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)); // Modifier (always private)

                var newParent = node.InsertNodesBefore(_autoProperty, new[] { privateField });

                return base.Visit(newParent);
            }
            return base.Visit(node);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax property)
        {
            if (property.IsEquivalentTo(this._autoProperty))
            {
                var privateFieldName = this.getPrivateFieldName();

                // Create the Getter of the property
                var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(
                                SyntaxFactory.Block(
                                    SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(privateFieldName))) // return field e.g. return _myProperty;
                             ).WithoutTrivia();

                var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithBody(
                                SyntaxFactory.Block(
                                    SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(privateFieldName), SyntaxFactory.IdentifierName("value")))) // Assignment e.g. _myProperty = value
                             ).WithoutTrivia();

                // Create a new property. Set the type and name
                var newProperty = SyntaxFactory.PropertyDeclaration(property.Type, property.Identifier.ValueText)
                    .AddModifiers(property.Modifiers.ToArray()); // use the modifier(s) of the existing property

                // Put together the property with our built getters & setters
                newProperty = newProperty.WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.List(new[] { getter, setter })));

                return newProperty;
            }

            return base.VisitPropertyDeclaration(property);
        }

        #region Private Methods

        private string getPrivateFieldName()
        {
            // Take the name of the property, make first letter lower case and prefix with _ e.g. "MyProperty" becomes "_myProperty".
            return String.Format("_{0}{1}", Char.ToLowerInvariant(this._autoProperty.Identifier.ValueText[0]), this._autoProperty.Identifier.ValueText.Substring(1));
        }

        #endregion
    }
}
