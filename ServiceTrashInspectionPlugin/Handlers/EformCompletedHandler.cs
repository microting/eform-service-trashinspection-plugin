using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Entities;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Factories;
using Rebus.Handlers;
using ServiceTrashInspectionPlugin.Messages;

namespace ServiceTrashInspectionPlugin.Handlers
{
    public class eFormCompletedHandler : IHandleMessages<eFormCompleted>
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly TrashInspectionPnDbContext _dbContext;

        public eFormCompletedHandler(eFormCore.Core sdkCore, TrashInspectionPnDbContext dbContext)
        {
            _dbContext = dbContext;
            _sdkCore = sdkCore;
        }

#pragma warning disable 1998
        public async Task Handle(eFormCompleted message)
        {

            Console.WriteLine("TrashInspection: We got a message : " + message.caseId);
            TrashInspectionCase trashInspectionCase = _dbContext.TrashInspectionCases.SingleOrDefault(x => x.SdkCaseId == message.caseId);
            if (trashInspectionCase != null)
            {
                Console.WriteLine("TrashInspection: The incoming case is a trash inspection related case");
                trashInspectionCase.Status = 100;
                trashInspectionCase.UpdatedAt = DateTime.Now;
                trashInspectionCase.Version += 1;
                await _dbContext.SaveChangesAsync();

                TrashInspection trashInspection = _dbContext.TrashInspections.SingleOrDefault(x => x.Id == trashInspectionCase.TrashInspectionId);
                trashInspection.Status = 100;
                trashInspection.UpdatedAt = DateTime.Now;
                trashInspection.Version += 1;
                await _dbContext.SaveChangesAsync();

                List<TrashInspectionCase> trashInspectionCases = _dbContext.TrashInspectionCases.Where(x => x.TrashInspectionId == trashInspection.Id).ToList();
                foreach (TrashInspectionCase inspectionCase in trashInspectionCases)
                {
                    if (_sdkCore.CaseDelete(inspectionCase.SdkCaseId))
                    {
                        inspectionCase.WorkflowState = eFormShared.Constants.WorkflowStates.Retracted;
                        inspectionCase.UpdatedAt = DateTime.Now;
                        inspectionCase.Version += 1;
                        await _dbContext.SaveChangesAsync();
                    }

                }
            }
            
        }
    }
}