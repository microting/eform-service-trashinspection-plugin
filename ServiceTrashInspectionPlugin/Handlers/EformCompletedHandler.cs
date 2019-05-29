using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using eFormData;
using eFormShared;
using Microsoft.EntityFrameworkCore;
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
                
                #region get case information

                Case_Dto caseDto = _sdkCore.CaseLookupMUId(message.caseId);
                var microtingUId = caseDto.MicrotingUId;
                var microtingCheckUId = caseDto.CheckUId;
                ReplyElement theCase = _sdkCore.CaseRead(microtingUId, microtingCheckUId);
                CheckListValue dataElement = (CheckListValue)theCase.ElementList[0];
                bool inspectionApproved = false;
                string approvedValue = "";
                string comment = "";
                Console.WriteLine("Trying to find the field with the approval value");
                foreach (var field in dataElement.DataItemList)
                {
                    Field f = (Field) field;
                    if (f.Label.Contains("Angiv om læs er Godkendt"))
                    {
                        Console.WriteLine($"The field is {f.Label}");
                        FieldValue fv = f.FieldValues[0];
                        String fieldValue = fv.Value;
                        inspectionApproved = (fieldValue == "1");
                        approvedValue = fieldValue;
                        Console.WriteLine($"We are setting the approved state to {inspectionApproved.ToString()}");
                    }

                    if (f.Label.Contains("Kommentar"))
                    {
                        Console.WriteLine($"The field is {f.Label}");
                        FieldValue fv = f.FieldValues[0];
                        String fieldValue = fv.Value;
                        comment = fieldValue;
                        Console.WriteLine($"We are setting the comment to {comment.ToString()}");
                    }
                }
                #endregion
                
                Console.WriteLine("TrashInspection: The incoming case is a trash inspection related case");
                trashInspectionCase.Status = 100;
                trashInspectionCase.UpdatedAt = DateTime.Now;
                trashInspectionCase.Version += 1;
                _dbContext.SaveChanges();

                TrashInspection trashInspection = _dbContext.TrashInspections.SingleOrDefault(x => x.Id == trashInspectionCase.TrashInspectionId);
                trashInspection.Status = 100;
                trashInspection.UpdatedAt = DateTime.Now;
                trashInspection.Version += 1;
                trashInspection.IsApproved = inspectionApproved;
                trashInspection.Comment = comment;
                trashInspection.ApprovedValue = approvedValue;
                _dbContext.SaveChanges();

                List<TrashInspectionCase> trashInspectionCases = _dbContext.TrashInspectionCases.Where(x => x.TrashInspectionId == trashInspection.Id).ToList();
                foreach (TrashInspectionCase inspectionCase in trashInspectionCases)
                {
                    if (_sdkCore.CaseDelete(inspectionCase.SdkCaseId))
                    {
                        inspectionCase.WorkflowState = eFormShared.Constants.WorkflowStates.Retracted;
                        inspectionCase.UpdatedAt = DateTime.Now;
                        inspectionCase.Version += 1;
                        _dbContext.SaveChanges();
                    }

                }

                #region get settings
                string callBackUrl = _dbContext.TrashInspectionPnSettings.SingleOrDefault(x => 
                    x.Name == "CallBackUrl")?.Value;                
                Console.WriteLine("callBackUrl is : " + callBackUrl);
                
                string callBackCredentialDomain = _dbContext.TrashInspectionPnSettings.SingleOrDefault(x => 
                    x.Name == "CallBackCredentialDomain")?.Value;                
                Console.WriteLine("callBackCredentialDomain is : " + callBackCredentialDomain);

                string callbackCredentialUserName = _dbContext.TrashInspectionPnSettings.SingleOrDefault(x => 
                    x.Name == "CallbackCredentialUserName")?.Value;                
                Console.WriteLine("callbackCredentialUserName is : " + callbackCredentialUserName);

                string callbackCredentialPassword = _dbContext.TrashInspectionPnSettings.SingleOrDefault(x => 
                    x.Name == "CallbackCredentialPassword")?.Value;                
                Console.WriteLine("callbackCredentialPassword is : " + callbackCredentialPassword);

                string callbackCredentialAuthType = _dbContext.TrashInspectionPnSettings.SingleOrDefault(x => 
                    x.Name == "CallbackCredentialAuthType")?.Value;                
                Console.WriteLine("callbackCredentialAuthType is : " + callbackCredentialAuthType);


                #endregion
                
                
                BasicHttpBinding basicHttpBinding =
                new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
                if (callbackCredentialAuthType == "NTLM")
                {
                    basicHttpBinding.Security.Transport.ClientCredentialType =
                        HttpClientCredentialType.Ntlm;
                    ChannelFactory<MicrotingWS_Port> factory =
                        new ChannelFactory<MicrotingWS_Port>(basicHttpBinding,
                            new EndpointAddress(
                                new Uri(callBackUrl)));
                    
                    if (callBackCredentialDomain != "...")
                    {
                        factory.Credentials.Windows.ClientCredential.Domain = callBackCredentialDomain;    
                    }
                    
                    factory.Credentials.Windows.ClientCredential.UserName = callbackCredentialUserName;
                    factory.Credentials.Windows.ClientCredential.Password = callbackCredentialPassword;

                    MicrotingWS_Port serviceProxy = factory.CreateChannel();
                    ((ICommunicationObject)serviceProxy).Open();

                    try
                    {
                        WeighingFromMicroting2 weighingFromMicroting2 =
                            new WeighingFromMicroting2(trashInspection.WeighingNumber, inspectionApproved);
                        Task<WeighingFromMicroting2_Result> result =
                            serviceProxy.WeighingFromMicroting2Async(weighingFromMicroting2);


                        Console.WriteLine("Result is " + result.Result.return_value);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("We got the following error: " + ex.Message);
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
}