using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.Application.UI.Actions.ActionManager;
using JetBrains.DocumentModel.DataContext;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.UnitTestFramework.Actions;
using JetBrains.ReSharper.UnitTestFramework.Criteria;
using JetBrains.ReSharper.UnitTestFramework.Elements;
using JetBrains.ReSharper.UnitTestFramework.Execution.Launch;
using JetBrains.ReSharper.UnitTestFramework.Persistence;
using ReSharperPlugin.SpecflowRiderPlugin.Psi;

namespace ReSharperPlugin.SpecflowRiderPlugin.UnitTestExplorers
{
    [SolutionComponent]
    public class SpecFlowTestsDataRules
    {
        private readonly IUnitTestElementRepository _unitTestElementRepository;

        public SpecFlowTestsDataRules(
            IUnitTestElementRepository unitTestElementRepository,
            IActionManager actionManager,
            Lifetime lifetime)
        {
            _unitTestElementRepository = unitTestElementRepository;
            actionManager.DataContexts.RegisterDataRule(lifetime, new DataRule<UnitTestElements>.AssertionDataRule("SpecFlowProjectFilesToUnitTestElements", UnitTestDataConstants.Elements.SELECTED, GetSpecFlowUnitTestElements));
        }

        private UnitTestElements GetSpecFlowUnitTestElements(IDataContext context)
        {
            using (CompilationContextCookie.GetExplicitUniversalContextIfNotSet())
            {
                IProjectModelElement[] data = context.GetData(ProjectModelDataConstants.PROJECT_MODEL_ELEMENTS);
                if (data == null || data.Length == 0)
                    return null;
                if (context.GetData(DocumentModelDataConstants.DOCUMENT) != null)
                    return null;

                var dependentFileTests = new List<IUnitTestElementCriterion>();
                var featureFileTests = new List<IUnitTestElement>();
                foreach (var projectModelElement in data)
                {
                    if (projectModelElement is IProjectFile projectFile)
                    {
                        if (projectFile.LanguageType.Is<GherkinProjectFileType>())
                        {
                            var dependentFiles = projectFile.GetDependentFiles();
                            if (dependentFiles.Count > 0)
                            {
                                dependentFileTests.AddRange(dependentFiles.Select(d => new ProjectFileCriterion(d)));
                            }
                            //old non SDK style project
                            else
                            {
                                var featureTests = _unitTestElementRepository.GetRelatedFeatureTests(projectFile.GetPrimaryPsiFile() as GherkinFile);
                                if (featureTests != null)
                                    featureFileTests.AddRange(featureTests);
                            }
                        }
                    }
                }

                if (dependentFileTests.Count == 0)
                {
                    if (featureFileTests.Count == 0)
                        return null;
                    return new UnitTestElements(TestAncestorCriterion.From(featureFileTests.Select(e => e.Id)), Array.Empty<IUnitTestElement>());
                }
                return dependentFileTests.Count == 1 ? 
                    new UnitTestElements(dependentFileTests.First(), Array.Empty<IUnitTestElement>()) 
                    : new UnitTestElements(DisjunctiveCriterion.From(dependentFileTests).Reduce(), Array.Empty<IUnitTestElement>());

            }
        }

    }
}