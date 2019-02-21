using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Entities;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Factories;
using Rebus.Handlers;
using ServiceTrashInspectionPlugin.Messages;
using TrashInspectionServiceReference;

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

                BasicHttpBinding basicHttpBinding =
                new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
                basicHttpBinding.Security.Transport.ClientCredentialType =
                    HttpClientCredentialType.Ntlm;

                ChannelFactory<MicrotingWS_Port> factory =
                    new ChannelFactory<TrashInspectionServiceReference.MicrotingWS_Port>(basicHttpBinding,
                    new EndpointAddress(
                        new Uri(@"...")));
                //factory.Credentials.Windows.ClientCredential.Domain = domain;
                //factory.Credentials.Windows.ClientCredential.UserName = user;
                //factory.Credentials.Windows.ClientCredential.Password = pass;
                MicrotingWS_Port serviceProxy = factory.CreateChannel();
                ((ICommunicationObject)serviceProxy).Open();
                //OperationContext opContext = new OperationContext((IClientChannel)serviceProxy);
                //OperationContext prevOpContext = OperationContext.Current; // Optional if there's no way this might already be set
                //OperationContext.Current = opContext;

                try
                {
                    WeighingFromMicroting2 vejningFraMicroTing2 = new WeighingFromMicroting2(trashInspection.WeighingNumber, true);
                    Task<WeighingFromMicroting2_Result> result = serviceProxy.WeighingFromMicroting2Async(vejningFraMicroTing2);


                    Console.WriteLine("Result is " + result.Result.return_value);

                }
                finally
                {
                    // cleanup
                    factory.Close();
                    ((ICommunicationObject)serviceProxy).Close();
                    // *** ENSURE CLEANUP *** \\
                    //CloseCommunicationObjects((ICommunicationObject)serviceProxy, factory);
                    //OperationContext.Current = prevOpContext; // Or set to null if you didn't capture the previous context
                }
            }
            
        }
    }
}