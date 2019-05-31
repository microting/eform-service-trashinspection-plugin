using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microting.eFormTrashInspectionBase.Infrastructure.Data;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Entities;
using Rebus.Handlers;
using ServiceTrashInspectionPlugin.Messages;

namespace ServiceTrashInspectionPlugin.Handlers
{
    public class eFormRetrievedHandler : IHandleMessages<eFormRetrieved>
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly TrashInspectionPnDbContext _dbContext;

        public eFormRetrievedHandler(eFormCore.Core sdkCore, TrashInspectionPnDbContext dbContext)
        {
            _dbContext = dbContext;
            _sdkCore = sdkCore;
        }

#pragma warning disable 1998
        public async Task Handle(eFormRetrieved message)
        {
            Console.WriteLine("TrashInspection: We got a message : " + message.caseId);
            TrashInspectionCase trashInspectionCase = _dbContext.TrashInspectionCases.SingleOrDefault(x => x.SdkCaseId == message.caseId);
            if (trashInspectionCase != null)
            {
                Console.WriteLine("TrashInspection: The incoming case is a trash inspection related case");
                if (trashInspectionCase.Status < 77)
                {
                    trashInspectionCase.Status = 77;
                    trashInspectionCase.UpdatedAt = DateTime.Now;
                    trashInspectionCase.Version += 1;
                    await _dbContext.SaveChangesAsync();    
                }

                TrashInspection trashInspection = _dbContext.TrashInspections.SingleOrDefault(x => x.Id == trashInspectionCase.TrashInspectionId);
                if (trashInspection != null)
                {
                    if (trashInspection.Status < 77)
                    {
                        trashInspection.Status = 77;
                        trashInspection.UpdatedAt = DateTime.Now;
                        trashInspection.Version += 1;
                        await _dbContext.SaveChangesAsync();                    
                    }    
                }
            }
        }
    }
}
