using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Zmq.Dto
{
    public class StateRequest : IStateRequest
    {

        public static readonly StateRequest Default = new StateRequest();
        public StateRequest()
        {
            Subject = string.Empty;
        }

        public StateRequest(string subject)
        {
            Subject = subject;
        }
        public string Subject { get; set; }
    }
}
