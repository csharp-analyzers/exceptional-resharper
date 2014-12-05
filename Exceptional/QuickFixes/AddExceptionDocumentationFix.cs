using System;
using JetBrains.Application;
using JetBrains.Application.Progress;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.Hotspots;
using JetBrains.ReSharper.Feature.Services.LiveTemplates.LiveTemplates;
using JetBrains.ReSharper.LiveTemplates;
using JetBrains.TextControl;
using JetBrains.Util;
using ReSharper.Exceptional.Highlightings;
using ReSharper.Exceptional.Models;
using ReSharper.Exceptional.Models.ExceptionsOrigins;

namespace ReSharper.Exceptional.QuickFixes
{
    [QuickFix(null, BeforeOrAfter.Before)]
    internal class AddExceptionDocumentationFix : SingleActionFix
    {
        private ExceptionNotDocumentedOptionalHighlighting Error { get; set; }

        public AddExceptionDocumentationFix(ExceptionNotDocumentedOptionalHighlighting error)
        {
            Error = error;
        }

        public override string Text
        {
            get
            {
                return String.Format(Resources.QuickFixInsertExceptionDocumentation,
                    Error.ThrownException.ExceptionType.GetClrName().ShortName);
            }
        }

        protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
        {
            var methodDeclaration = Error.ThrownException.AnalyzeUnit;
            var exceptionDescription = Error.ThrownException.ExceptionDescription;

            if (Error.ThrownException.ExceptionsOrigin is ThrowStatementModel && 
                Error.ThrownException.ExceptionType.GetClrName().FullName == "System.ArgumentNullException")
                exceptionDescription = string.Format("The value of '{0}' cannot be null. ", exceptionDescription);

            var insertedExceptionModel = methodDeclaration.DocumentationBlock.AddExceptionDocumentation(
                Error.ThrownException.ExceptionType, exceptionDescription, progress);

            if (insertedExceptionModel == null)
                return null;

            return MarkInsertedDescription(solution, insertedExceptionModel);
        }

        private Action<ITextControl> MarkInsertedDescription(ISolution solution, ExceptionDocCommentModel insertedExceptionModel)
        {
            var exceptionCommentRange = insertedExceptionModel.GetMarkerRange();
            if (exceptionCommentRange == DocumentRange.InvalidRange)
                return null;

            var copyExceptionDescription =
                string.IsNullOrEmpty(insertedExceptionModel.ExceptionDescription) ||
                insertedExceptionModel.ExceptionDescription.Contains("[MARKER]");

            var exceptionDescription = copyExceptionDescription ? "Condition" : insertedExceptionModel.ExceptionDescription;

            var nameSuggestionsExpression = new NameSuggestionsExpression(new[] {exceptionDescription});
            var field = new TemplateField("name", nameSuggestionsExpression, 0);
            var fieldInfo = new HotspotInfo(field, exceptionCommentRange);

            return textControl =>
            {
                var hotspotSession = Shell.Instance.GetComponent<LiveTemplatesManager>()
                    .CreateHotspotSessionAtopExistingText(
                        solution, TextRange.InvalidRange, textControl, LiveTemplatesManager.EscapeAction.LeaveTextAndCaret,
                        new[] {fieldInfo});

                hotspotSession.Execute();
            };
        }
    }
}