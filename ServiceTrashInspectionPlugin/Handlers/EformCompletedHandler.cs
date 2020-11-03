/*
The MIT License (MIT)
Copyright (c) 2007 - 2019 Microting A/S
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microting.eForm.Dto;
using Microting.eForm.Infrastructure.Constants;
using Microting.eForm.Infrastructure.Models;
using Microting.eFormTrashInspectionBase.Infrastructure.Data;
using Microting.eFormTrashInspectionBase.Infrastructure.Data.Entities;
using Rebus.Handlers;
using ServiceTrashInspectionPlugin.Infrastructure.Helpers;
using ServiceTrashInspectionPlugin.Messages;
using TrashInspectionServiceReference;

namespace ServiceTrashInspectionPlugin.Handlers
{
    public class eFormCompletedHandler : IHandleMessages<eFormCompleted>
    {
        private readonly eFormCore.Core _sdkCore;
        private readonly TrashInspectionPnDbContext _dbContext;

        public eFormCompletedHandler(eFormCore.Core sdkCore, DbContextHelper dbContextHelper)
        {
            _dbContext = dbContextHelper.GetDbContext();
            _sdkCore = sdkCore;
        }

        #pragma warning disable 1998
        public async Task Handle(eFormCompleted message)
        {

            Console.WriteLine("[DBG] TrashInspection: We got a message : " + message.caseId);
            TrashInspectionCase trashInspectionCase =
                _dbContext.TrashInspectionCases.SingleOrDefault(x => x.SdkCaseId == message.caseId.ToString());
            if (trashInspectionCase != null)
            {
                
                #region get case information

                CaseDto caseDto = await _sdkCore.CaseLookupMUId(message.caseId);
                var microtingUId = caseDto.MicrotingUId;
                var microtingCheckUId = caseDto.CheckUId;
                ReplyElement theCase = await _sdkCore.CaseRead((int)microtingUId, (int)microtingCheckUId);
                CheckListValue dataElement = (CheckListValue)theCase.ElementList[0];
                bool inspectionApproved = false;
                string approvedValue = "";
                string comment = "";
                Console.WriteLine("[DBG] Trying to find the field with the approval value");
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
                        Console.WriteLine($"[DBG] We are setting the approved state to {inspectionApproved.ToString()}");
                    }

                    if (f.Label.Equals("Kommentar"))
                    {
                        Console.WriteLine($"[DBG] The field is {f.Label}");
                        FieldValue fv = f.FieldValues[0];
                        String fieldValue = fv.Value;
                        comment = fieldValue;
                        Console.WriteLine($"[DBG] We are setting the comment to {comment.ToString()}");
                    }
                }
                #endregion
                
                Console.WriteLine("TrashInspection: The incoming case is a trash inspection related case");
                trashInspectionCase.Status = 100;
                await trashInspectionCase.Update(_dbContext);

                TrashInspection trashInspection =
                    _dbContext.TrashInspections.SingleOrDefault(x => x.Id == trashInspectionCase.TrashInspectionId);
                if (trashInspection != null)
                {
                    trashInspection.Status = 100;
                    trashInspection.IsApproved = inspectionApproved;
                    trashInspection.Comment = comment;
                    trashInspection.ApprovedValue = approvedValue;
                    trashInspection.InspectionDone = true;
                    await trashInspection.Update(_dbContext);

                    List<TrashInspectionCase> trashInspectionCases = _dbContext.TrashInspectionCases
                        .Where(x => x.TrashInspectionId == trashInspection.Id).ToList();
                    foreach (TrashInspectionCase inspectionCase in trashInspectionCases)
                    {
                        if (await _sdkCore.CaseDelete(int.Parse(inspectionCase.SdkCaseId)))
                        {
                            inspectionCase.WorkflowState = Constants.WorkflowStates.Retracted;
                            await inspectionCase.Update(_dbContext);
                        }
                    }

                    #region get settings

                    string callBackUrl = _dbContext.PluginConfigurationValues
                        .SingleOrDefault(x => x.Name == "TrashInspectionBaseSettings:callBackUrl")?.Value;
                    Console.WriteLine("[DBG] callBackUrl is : " + callBackUrl);

                    string callBackCredentialDomain = _dbContext.PluginConfigurationValues.SingleOrDefault(x =>
                        x.Name == "TrashInspectionBaseSettings:CallBackCredentialDomain")?.Value;
                    Console.WriteLine("[DBG] callBackCredentialDomain is : " + callBackCredentialDomain);

                    string callbackCredentialUserName = _dbContext.PluginConfigurationValues.SingleOrDefault(x =>
                        x.Name == "TrashInspectionBaseSettings:callbackCredentialUserName")?.Value;
                    Console.WriteLine("[DBG] callbackCredentialUserName is : " + callbackCredentialUserName);

                    string callbackCredentialPassword = _dbContext.PluginConfigurationValues.SingleOrDefault(x =>
                        x.Name == "TrashInspectionBaseSettings:CallbackCredentialPassword")?.Value;
                    Console.WriteLine("[DBG] callbackCredentialPassword is : " + callbackCredentialPassword);

                    string callbackCredentialAuthType = _dbContext.PluginConfigurationValues.SingleOrDefault(x =>
                        x.Name == "TrashInspectionBaseSettings:CallbackCredentialAuthType")?.Value;
                    Console.WriteLine("[DBG] callbackCredentialAuthType is : " + callbackCredentialAuthType);

                    Console.WriteLine($"[DBG] trashInspection.WeighingNumber is {trashInspection.WeighingNumber}");
                    #endregion

                    switch (callbackCredentialAuthType)
                    {
                        case "NTLM":
                            await CallUrlNtlmAuth(callBackUrl, callBackCredentialDomain, callbackCredentialUserName,
                                callbackCredentialPassword, trashInspection, inspectionApproved);
                            break;
                        case "basic":
                        default:
                            await CallUrlBaiscAuth(callBackUrl, callBackCredentialDomain, callbackCredentialUserName,
                                callbackCredentialPassword, trashInspection, inspectionApproved);
                            break;
                    }
                }
            }
            
        }

        private async Task CallUrlBaiscAuth(string callBackUrl, string callBackCredentialDomain, 
            string callbackCredentialUserName, string callbackCredentialPassword, TrashInspection trashInspection,
            bool inspectionApproved)
        {

            ChannelFactory<MicrotingWS_Port> factory;
            MicrotingWS_Port serviceProxy;
            BasicHttpBinding basicHttpBinding =
                            new BasicHttpBinding();
                        basicHttpBinding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
                        basicHttpBinding.Security.Transport.ClientCredentialType =
                        HttpClientCredentialType.Basic;
                        factory =
                            new ChannelFactory<MicrotingWS_Port>(basicHttpBinding,
                                new EndpointAddress(
                                    new Uri(callBackUrl)));
                        
                        if (callBackCredentialDomain != "...")
                        {
                            factory.Credentials.Windows.ClientCredential.Domain = callBackCredentialDomain;    
                        }

                        factory.Credentials.UserName.UserName = callbackCredentialUserName;
                        factory.Credentials.UserName.Password = callbackCredentialPassword;
                        
                        serviceProxy = factory.CreateChannel();
                        ((ICommunicationObject)serviceProxy).Open();

                        try
                        {
                            WeighingFromMicroting2 weighingFromMicroting2 =
                                new WeighingFromMicroting2(trashInspection.WeighingNumber, inspectionApproved);
                            Task<WeighingFromMicroting2_Result> result =
                                serviceProxy.WeighingFromMicroting2Async(weighingFromMicroting2);


                            Console.WriteLine("[DBG] Result is " + result.Result.return_value);
                            trashInspection.SuccessMessageFromCallBack = result.Result.return_value;
                            trashInspection.ResponseSendToCallBackUrl = true;
                            await trashInspection.Update(_dbContext);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ERR] We got the following error: " + ex.Message);
                            trashInspection.ErrorFromCallBack = ex.Message;
                            await trashInspection.Update(_dbContext);
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

        private async Task CallUrlNtlmAuth(string callBackUrl, string callBackCredentialDomain,
            string callbackCredentialUserName, string callbackCredentialPassword, TrashInspection trashInspection,
            bool inspectionApproved)
        {

            ChannelFactory<MicrotingWS_Port> factory;
            MicrotingWS_Port serviceProxy;
            BasicHttpBinding basicHttpBindingntlm =
                            new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
                        basicHttpBindingntlm.Security.Transport.ClientCredentialType =
                        HttpClientCredentialType.Ntlm;
                        factory =
                            new ChannelFactory<MicrotingWS_Port>(basicHttpBindingntlm,
                                new EndpointAddress(
                                    new Uri(callBackUrl)));
                        
                        if (callBackCredentialDomain != "...")
                        {
                            factory.Credentials.Windows.ClientCredential.Domain = callBackCredentialDomain;    
                        }
                        
                        factory.Credentials.Windows.ClientCredential.UserName = callbackCredentialUserName;
                        factory.Credentials.Windows.ClientCredential.Password = callbackCredentialPassword;

                        serviceProxy = factory.CreateChannel();
                        ((ICommunicationObject)serviceProxy).Open();

                        try
                        {
                            WeighingFromMicroting2 weighingFromMicroting2 =
                                new WeighingFromMicroting2(trashInspection.WeighingNumber, inspectionApproved);
                            Task<WeighingFromMicroting2_Result> result =
                                serviceProxy.WeighingFromMicroting2Async(weighingFromMicroting2);


                            Console.WriteLine("[DBG] Result is " + result.Result.return_value);
                            trashInspection.SuccessMessageFromCallBack = result.Result.return_value;
                            trashInspection.ResponseSendToCallBackUrl = true;
                            await trashInspection.Update(_dbContext);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ERR] We got the following error: " + ex.Message);
                            trashInspection.ErrorFromCallBack = ex.Message;
                            await trashInspection.Update(_dbContext);
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