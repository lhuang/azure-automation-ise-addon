﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Automation;
using Microsoft.Azure.Management.Automation.Models;

namespace AutomationISE.Model
{
    /*
     * Responsible for syncing runbooks between the cloud and on disk.
     */
    public static class AutomationRunbookManager
    {
        public static async Task UploadRunbookAsDraft(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookCreateOrUpdateDraftProperties draftProperties = new RunbookCreateOrUpdateDraftProperties("Script", new RunbookDraft());
            RunbookCreateOrUpdateDraftParameters draftParams = new RunbookCreateOrUpdateDraftParameters(draftProperties);
            draftParams.Name = runbook.Name;
            //TODO: Read this from account location
            draftParams.Location = "East US 2";

            automationManagementClient.Runbooks.CreateOrUpdateWithDraft(resourceGroupName, accountName, draftParams);
            /* Update the runbook content from .ps1 file */
            RunbookDraftUpdateParameters draftUpdateParams = new RunbookDraftUpdateParameters()
            {
                Name = runbook.Name,
                Stream = File.ReadAllText(runbook.localFileInfo.FullName)
            };
            await automationManagementClient.RunbookDraft.UpdateAsync(resourceGroupName, accountName, draftUpdateParams);
        }

        public static async Task<LongRunningOperationResultResponse> PublishRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookDraftPublishParameters publishParams = new RunbookDraftPublishParameters
            {
                Name = runbook.Name,
                PublishedBy = "ISE User: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name
            };
            LongRunningOperationResultResponse resultResponse = await automationManagementClient.RunbookDraft.PublishAsync(resourceGroupName, accountName, publishParams);
            //TODO: update runbook object
            return resultResponse;
        }

        public static async Task DownloadRunbook(AutomationRunbook runbook, AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName)
        {
            RunbookContentResponse runbookContent = await automationManagementClient.Runbooks.ContentAsync(resourceGroupName, accountName, runbook.Name);
            String runbookFilePath = System.IO.Path.Combine(workspace, runbook.Name + ".ps1");
            File.WriteAllText(runbookFilePath, runbookContent.Stream.ToString());
            
            //TODO: do this with a setter, so the status update properly
            runbook.localFileInfo = new FileInfo(runbookFilePath);
        }

        public static async Task<ISet<AutomationRunbook>> GetAllRunbookMetadata(AutomationManagementClient automationManagementClient, string workspace, string resourceGroupName, string accountName)
        {
            ISet<AutomationRunbook> result = new SortedSet<AutomationRunbook>();
            IList<Runbook> cloudRunbooks = await DownloadRunbookMetadata(automationManagementClient, resourceGroupName, accountName);
            
            /* Dictionary of (filename, filepath) tuples found on disk. This will come in handy */
            string[] localRunbookFilePaths = Directory.GetFiles(workspace, "*.ps1");
            Dictionary<string, string> filePathForRunbook = new Dictionary<string, string>();
            foreach (string path in localRunbookFilePaths)
            {
                filePathForRunbook.Add(System.IO.Path.GetFileNameWithoutExtension(path), path);
            }
            /* Start by checking the downloaded runbooks */
            foreach (Runbook cloudRunbook in cloudRunbooks)
            {
                if (filePathForRunbook.ContainsKey(cloudRunbook.Name))
                {
                    result.Add(new AutomationRunbook(new FileInfo(filePathForRunbook[cloudRunbook.Name]), cloudRunbook));
                }
                else
                {
                    result.Add(new AutomationRunbook(cloudRunbook));
                }
            }
            /* Now find runbooks on disk that aren't yet accounted for */
            foreach (string localRunbookName in filePathForRunbook.Keys)
            {
                //Not great, but works for now
                if (result.FirstOrDefault(x => x.Name == localRunbookName) == null)
                {
                    result.Add(new AutomationRunbook(new FileInfo(filePathForRunbook[localRunbookName])));
                }
            }
            return result;
        }

        private static async Task<IList<Runbook>> DownloadRunbookMetadata(AutomationManagementClient automationManagementClient, string resourceGroupName, string accountName)
        {
            RunbookListResponse cloudRunbooks = await automationManagementClient.Runbooks.ListAsync(resourceGroupName, accountName);
            return cloudRunbooks.Runbooks;
        }
    }
}
