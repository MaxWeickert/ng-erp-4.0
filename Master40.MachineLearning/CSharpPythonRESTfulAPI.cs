using System;
using System.Drawing;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using RestSharp;
using Master40.MachineLearning.DataStuctures;

namespace Master40.MachineLearning
{
    public class CSharpPythonRESTfulAPI : ICSharpPythonRESTfulAPI
    {
        public string CSharpPythonRestfulApiPredictCycleTime(string uirWebAPI, SimulationKpisReshaped kpis, out string exceptionMessage)
        {
            exceptionMessage = string.Empty;
            string webResponse = string.Empty;

            string predictedCycleTime = string.Empty;

            try
            {
                Uri uri = new Uri(uirWebAPI);
                WebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    dynamic kpi_object = new JObject();
                    kpi_object.Assembly = kpis.Assembly;
                    kpi_object.Material = kpis.Material;
                    kpi_object.TotalWork = kpis.TotalWork;
                    kpi_object.TotalSetup = kpis.TotalSetup;
                    kpi_object.SumDuration = kpis.SumDuration;
                    kpi_object.SumOperations = kpis.SumOperations;
                    kpi_object.ProductionOrders = kpis.ProductionOrders;
                    kpi_object.CycleTime = kpis.CycleTime;
                    streamWriter.Write(kpi_object.ToString());
                }
                HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
                {
                    webResponse = streamReader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                exceptionMessage = $"An error occurred. {ex.Message}";
            }
            return webResponse;
        }
    }
}
