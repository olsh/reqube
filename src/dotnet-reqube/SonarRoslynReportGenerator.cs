﻿using ReQube.Models.ReSharper;
using ReQube.Models.SonarQube;
using ReQube.Models.SonarQube.Roslyn;
using ReQube.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;

using static ReQube.Models.Constants;

namespace ReQube
{
    public class SonarRoslynReportGenerator : SonarBaseReportGenerator
    {
        public override List<ISonarReport> Generate(Report reSharperReport)
        {
            var reports = new List<ISonarReport>();

            var issueTypes = reSharperReport.IssueTypes.ToDictionary(t => t.Id, type => type);
            var lastLoadedFilePath = "";
            var lastLoadedFileContent = "";
            string[] lastLoadedFileLines = null;

            foreach (var project in reSharperReport.Issues)
            {
                var rulesById = new Dictionary<string, Rule>();
                var report = new SonarRoslynReport() { ProjectName = project.Name };
                reports.Add(report);

                var run = new Run();
                report.Runs.Add(run);

                foreach (var issue in project.Issue)
                {
                    ReadIssueFile(issue.File, ref lastLoadedFilePath, ref lastLoadedFileContent, ref lastLoadedFileLines);

                    var issueType = issueTypes[issue.TypeId];
                    var ruleId = $"ReSharper.{issue.TypeId}";
                    var details = string.IsNullOrWhiteSpace(issueType.WikiUrl)
                        ? string.Empty 
                        : $" Click <a href=\"{issueType.WikiUrl}\" target=\"_blank\">here</a> for details.";

                    var rule = new Rule
                    {
                        Id = ruleId,
                        ShortDescription = issue.Message,
                        FullDescription = $"{issueType.Description}.{details}",
                        DefaultLevel = ReSharperToSonarRoslynSeverityMap[issueType.Severity],
                        HelpUrl = issueType.WikiUrl
                    };

                    rulesById.TryAdd(rule.Id, rule);

                    var result = new Result
                    {
                        RuleId = ruleId,
                        Message = issue.Message,
                        Level = ReSharperToSonarRoslynSeverityMap[issueType.Severity]

                    };

                    var line = ((ISonarReportGenerator)this).GetSonarLine(issue.Line);
                    var (startColumn, endColumn) = FindLineOffset(issue.Offset, line, lastLoadedFilePath, lastLoadedFileContent, lastLoadedFileLines);

                    var textRange = new TextRange
                                        {
                                            StartLine = line,
                                            EndLine = line,
                                        };
                    if (startColumn.HasValue && endColumn.HasValue)
                    {
                        // line offset in Roslyn is 1-based
                        textRange.StartColumn = startColumn + 1;
                        textRange.EndColumn = endColumn + 1;
                    }

                    result.Locations.Add(
                        new Location
                        {
                            ResultFile = new ResultFile
                            {
                                Uri = FileUtils.FilePathToFileUrl(issue.File),
                                Region = textRange
                            }
                        });

                    run.Results.Add(result);
                }

                foreach (Rule rule in rulesById.Values)
                {
                    run.Rules.Add(rule.Id, rule);
                }
            }

            return reports;
        }
    }
}
