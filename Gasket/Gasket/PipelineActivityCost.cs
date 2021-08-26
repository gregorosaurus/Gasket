using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gasket
{
    public class PipelineActivityCost
    {
        public string PipelineName { get; set; }
        public string PipelineRunId { get; set; }
        public string ActivityType { get; set; }
        public DateTime? ActivityStartTime { get; set; }
        public DateTime? ActivityEndTime { get; set; }
        public double? ActivityRunTimeHours
        {
            get
            {
                return (ActivityEndTime - ActivityStartTime)?.TotalHours ?? (double?)null;
            }
        }
        public double BilledDurationHours { get; set; }
        public string BilledMetreType { get; set; }
        public string BilledUnit { get; set; }

        /// <summary>
        /// Returns the amount theoretically billed using 
        /// this pricing
        /// https://azure.microsoft.com/en-in/pricing/details/synapse-analytics/
        /// </summary>
        public double BilledAmount
        {
            get
            {
                if (BilledUnit == "Hours" && BilledMetreType != "AzureIR")
                {
                    return 1.5 / 1000.0 * BilledDurationHours;
                }
                else if (BilledUnit == "Hours" && BilledMetreType == "AzureIR")
                {
                    return 1.0 / 1000.0 * BilledDurationHours;
                }
                else if (BilledUnit == "DIUHours" && BilledMetreType != "AzureIR")
                {
                    return 0.10 * BilledDurationHours;
                }
                else if (BilledUnit == "DIUHours" && BilledMetreType == "AzureIR")
                {
                    return 0.25 * BilledDurationHours;
                }

                return 0;
            }
        }
    }
}
