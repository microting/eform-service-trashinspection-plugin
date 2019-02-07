using System;

namespace ServiceTrashInspectionPlugin.Messages
{
    public class EformCompleted
    {
        public string caseId { get; protected set; }

        public EformCompleted(string caseId)
        {
            this.caseId = caseId;
        }
    }
}