using HtmlAgilityPack;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Upwork.LeviFreedman.polkcountyiowa
{
    public class DataModel
    {
        public int Id { get; set; }
        public string SalesDate { get; set; }
        public string PlainTiff { get; set; }
        public string Defendant { get; set; }
        public string SheriffNumber { get; set; }
        public double ApproxJudgment { get; set; }
        public string RedemptionPeriod { get; set; }
        public string Address { get; set; }
        public double Zestimate { get; set; }
        public string IndexValue { get; set; }
        public decimal TaxAssesment { get; set; }
        public double Zestimate_Approx_Judgement_Diff { get; set; }
        public double Zestimate_Approx_Judgement_Division { get; set; }

    }

    public class ReturnedResult
    {
        public string Zestimate { get; set; }
        public string ZillowHomeValueIndex { get; set; }
        public string TaxAssestment { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            List<DataModel> records = new List<DataModel>();
            try
            {
                var client = new RestClient("https://socms.polkcountyiowa.gov/");
                var request = new RestRequest("sheriffsaleviewer/Home/PropertyListJson", Method.POST);
                request.AddParameter("draw", 1);
                request.AddParameter("start", 0);
                request.AddParameter("length", 100);
                request.AddParameter("isOpenStatus", true);

                var responce = client.Execute<RootObject>(request);
                var data = responce.Data.data;

                Console.WriteLine("Total Records: " + data.Count);
                if (data.Count > 0)
                {
                    HtmlWeb web = new HtmlWeb();
                    int i = 1;
                    foreach (var item in data)
                    {
                        Console.WriteLine("===============================PROCESSING RECORD===============================");
                        //Get Details of each property
                        string url = "https://apps.polkcountyiowa.gov/sheriffsaleviewer/Home/Detail/" + item.propertyId;
                        var doc = web.Load(url);

                        var table = doc.DocumentNode.SelectSingleNode("/html/body/div/div[3]/div/table/tbody");
                        DataModel model = new DataModel() { Id = i };
                        Console.WriteLine("Getting details...");
                        if (table != null)
                        {

                            foreach (var entry in table.ChildNodes.Where(x => x.Name != "#text"))
                            {
                                try
                                {
                                    HtmlDocument row = new HtmlDocument();
                                    row.LoadHtml(entry.InnerHtml);

                                    var title = row.DocumentNode.SelectSingleNode("/th[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                    title = title.Trim();
                                    string value = "";
                                    switch (title)
                                    {
                                        case "Sheriff Number":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.SheriffNumber = value.Trim();
                                            break;
                                        case "Approximate Judgment":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.ApproxJudgment = Convert.ToDouble(value.Replace("$", "").Replace(",", "").Trim());
                                            break;
                                        case "Sales Date":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.SalesDate = value.Trim();
                                            break;
                                        case "Plaintiff":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.PlainTiff = value.Trim();
                                            break;
                                        case "Defendant":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.Defendant = value.Trim();
                                            break;
                                        case "Address":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.Address = value.Trim();
                                            break;
                                        case "Redemption Period":
                                            value = row.DocumentNode.SelectSingleNode("/td[1]").InnerText.Replace("\n", "").Replace("\r", "");
                                            model.RedemptionPeriod = value.Trim();
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("Unable to compile record.\nReason:" + ex.Message);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Record details page not found");
                            model.SheriffNumber = item.referenceNumber.ToString();
                            model.SalesDate = item.salesDate.ToString("MM/dd/yyyy");
                            model.PlainTiff = item.plaintiff;
                            model.Defendant = item.defendant;
                            model.Address = item.propertyAddress;
                        }

                        if (!string.IsNullOrEmpty(model.Address))
                        {
                            Console.WriteLine("Getting Data from zillow...");
                            ReturnedResult dataModel = GetZestimateAPI(model.Address);
                            model.Zestimate = (string.IsNullOrEmpty(dataModel.Zestimate)) ? 0 : Math.Round(Convert.ToDouble(dataModel.Zestimate));
                            model.TaxAssesment = string.IsNullOrEmpty(dataModel.TaxAssestment) ? 0 : Convert.ToDecimal(dataModel.TaxAssestment);
                            model.IndexValue = dataModel.ZillowHomeValueIndex;

                            if (model.Zestimate == 0)
                            {
                                if (model.TaxAssesment != 0)
                                {
                                    model.Zestimate_Approx_Judgement_Diff = Math.Round((double)model.TaxAssesment - model.ApproxJudgment, 2);
                                    if (model.TaxAssesment > 0)
                                    {
                                        model.Zestimate_Approx_Judgement_Division = model.ApproxJudgment / (double)model.TaxAssesment;
                                    }
                                }
                            }
                            else
                            {
                                model.Zestimate_Approx_Judgement_Diff = Math.Round(model.Zestimate - model.ApproxJudgment, 2);
                                if (model.Zestimate > 0)
                                {
                                    model.Zestimate_Approx_Judgement_Division = model.ApproxJudgment / model.Zestimate;
                                }
                            }
                        }
                        else
                            Console.WriteLine("Address not found. So we cant get zestimate");

                        records.Add(model);
                        i += 1;
                    }

                    //Console.WriteLine("Fetching data from realtor.com. This may take few minutes...");
                    //records = GetRealtorEstimate(records);
                    //foreach (var model in records)
                    //{
                    //    model.Zestimate_Approx_Judgement_Diff = Math.Round(model.Zestimate - model.ApproxJudgment, 2);
                    //    if (model.Zestimate > 0)
                    //    {
                    //        model.Zestimate_Approx_Judgement_Division = model.ApproxJudgment / model.Zestimate;
                    //    }
                    //}

                    List<string> entries = new List<string>();
                    entries.Add(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\"",
                        "SalesDate", "PlainTiff", "Defendant", "SheriffNumber", "ApproxJudgment", "RedemptionPeriod", "Address"
                        , "Zestimate", "Zestimate_Approx_Judgement_Diff", "Zestimate_Approx_Judgement_Division", "Tax Assestment", "Home Index Value"));
                    foreach (var item in records)
                    {
                        entries.Add(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\"",
                         item.SalesDate, item.PlainTiff, item.Defendant, item.SheriffNumber, item.ApproxJudgment, item.RedemptionPeriod, item.Address
                         , item.Zestimate, item.Zestimate_Approx_Judgement_Diff, item.Zestimate_Approx_Judgement_Division, item.TaxAssesment, item.IndexValue));
                    }

                    DateTime today = DateTime.Now;
                    var time = string.Format("{0}{1}{2}{3}{4}{5}", today.Year, today.Month, today.Day, today.Hour, today.Minute, today.Second);

                    File.WriteAllLines("results-" + time + ".csv", entries);
                }
                else
                    Console.WriteLine("No record found. Please try again another time.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to fetch data.\nReason: " + ex.Message);
            }
            Console.WriteLine("Operation Completed. Press any key to exit.");
            Console.ReadKey();
        }

        private static ReturnedResult GetZestimateAPI(string address)
        {
            ReturnedResult result = new ReturnedResult();
            string[] parts = address.Split(',');
            switch (parts.Count())
            {
                case 3:
                    break;
                case 4:
                    break;
                case 5:
                    break;
                default:
                    break;
            }

            try
            {
                var client = new RestClient("https://www.zillow.com/");
                var request = (parts.Count() == 5) ? new RestRequest("webservice/GetDeepSearchResults.htm?zws-id=X1-ZWz17v7h6igwzv_75222&address=" + parts[0] + "&citystatezip=" + string.Format("{0},{1},{2}", parts[2], parts[3], parts[4]) + "", Method.GET)
                    : new RestRequest("webservice/GetDeepSearchResults.htm?zws-id=X1-ZWz17v7h6igwzv_75222&address=" + parts[0] + "&citystatezip=" + string.Format("{0},{1},{2}", parts[1], parts[2], parts[3]) + "", Method.GET);
                request.AddHeader("Accept", "application/json");
                var responce = client.Execute(request);
                if (responce.Content.Contains("<zestimate>"))
                {
                    try
                    {
                        var text = responce.Content.Substring(responce.Content.IndexOf("<zestimate>"));
                        var another = text.Substring(text.IndexOf("</zestimate>") + 12);
                        text = text.Replace(another, "");
                        XmlSerializer serializer = new XmlSerializer(typeof(Zestimate), new XmlRootAttribute("zestimate"));
                        StringReader stringReader = new StringReader(text);
                        Zestimate zen = (Zestimate)serializer.Deserialize(stringReader);
                        result.Zestimate = zen.Amount.Text;
                        //return zestimate;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get zestimate. Exception: " + ex.Message);
                    }
                }

                if (responce.Content.Contains("<taxAssessment>"))
                {
                    try
                    {
                        var text = responce.Content.Substring(responce.Content.IndexOf("<taxAssessment>"));
                        var another = text.Substring(text.IndexOf("</taxAssessment>") + 16);
                        text = text.Replace(another, "");
                        XmlSerializer serializer = new XmlSerializer(typeof(string), new XmlRootAttribute("taxAssessment"));
                        StringReader stringReader = new StringReader(text);
                        result.TaxAssestment = (string)serializer.Deserialize(stringReader);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get tax assestment. Exception: " + ex.Message);
                    }
                    //  result.Zestimate = tax.Text;
                    //return zestimate;
                }

                if (responce.Content.Contains("<localRealEstate>"))
                {
                    try
                    {
                        var text = responce.Content.Substring(responce.Content.IndexOf("<localRealEstate>"));
                        var another = text.Substring(text.IndexOf("</localRealEstate>") + 18);
                        text = text.Replace(another, "");
                        XmlSerializer serializer = new XmlSerializer(typeof(LocalRealEstate), new XmlRootAttribute("localRealEstate"));
                        StringReader stringReader = new StringReader(text);
                        var m = (LocalRealEstate)serializer.Deserialize(stringReader);

                        result.ZillowHomeValueIndex = m.Region[0].ZindexValue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to get z-index-value. Exception: " + ex.Message);
                    }
                    //  result.Zestimate = tax.Text;
                    //return zestimate;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to get zestimate.\nReason: " + ex.Message);
            }

            return result;
        }

        //private static void GetMoreDetails()
        //{
        //    var client = new RestClient("https://www.zillow.com/");
        //    var request = (parts.Count() == 5) ? new RestRequest("webservice/GetSearchResults.htm?zws-id=X1-ZWz17v7h6igwzv_75222&address=" + parts[0] + "&citystatezip=" + string.Format("{0},{1},{2}", parts[2], parts[3], parts[4]) + "", Method.GET)
        //        : new RestRequest("webservice/GetSearchResults.htm?zws-id=X1-ZWz17v7h6igwzv_75222&address=" + parts[0] + "&citystatezip=" + string.Format("{0},{1},{2}", parts[1], parts[2], parts[3]) + "", Method.GET);
        //    request.AddHeader("Accept", "application/json");
        //    var responce = client.Execute(request);
        //    if (responce.Content.Contains("<zestimate>"))
        //    {
        //        var text = responce.Content.Substring(responce.Content.IndexOf("<zestimate>"));
        //        var another = text.Substring(text.IndexOf("</zestimate>") + 12);
        //        text = text.Replace(another, "");
        //        XmlSerializer serializer = new XmlSerializer(typeof(Zestimate), new XmlRootAttribute("zestimate"));
        //        StringReader stringReader = new StringReader(text);
        //        Zestimate result = (Zestimate)serializer.Deserialize(stringReader);
        //        zestimate = result.Amount.Text;
        //        return zestimate;
        //    }
        //    else
        //    {
        //        Console.WriteLine("Unable to get zestimate of address {0}", (parts.Count() == 5)
        //            ? parts[0] + " " + parts[2] + " " + parts[3] + " " + parts[4]
        //            : parts[0] + " " + parts[1] + " " + parts[2] + " " + parts[3]);
        //        return "";
        //    }
        //}

        //static List<DataModel> GetRealtorEstimate(List<DataModel> model)
        //{

        //       ChromeOptions options = new ChromeOptions();
        //    options.AddArguments(new List<string>()
        //      {
        //        "--silent-launch",
        //        "--no-startup-window",
        //        "no-sandbox",
        //        "headless"
        //      });
        //    ChromeDriverService defaultService = ChromeDriverService.CreateDefaultService();
        //    defaultService.HideCommandPromptWindow = true;
        //    using (IWebDriver webDriver = (IWebDriver)new ChromeDriver(defaultService, options))
        //    {
        //        string url = "https://www.realtor.com/realestateandhomes-detail/8601-Westown-Pkwy-Unit-13105_West-Des-Moines_IA_50266_M75785-31294";
        //        webDriver.Navigate().GoToUrl(url);
        //        string lastValue = "";
        //        foreach (var address in model)
        //        {
        //            if (address.Zestimate != 0)
        //                continue;
        //            try
        //            {
        //                var searchField = webDriver.FindElement(By.XPath("//*[@id=\"searchBox\"]"));
        //                searchField.Clear();
        //                searchField.SendKeys(address.Address);

        //                //var searchBtn = webDriver.FindElement(By.XPath("/html/body/div[5]/div[2]/section/div/div[1]/div[2]/div[1]/span/button[2]"));
        //                //searchBtn.Click();
        //                searchField.SendKeys(Keys.Return);
        //                Thread.Sleep(5000);
        //                var price = webDriver.FindElement(By.XPath("//*[@id=\"ldp-pricewrap\"]/div/div/span[2]"));

        //                //If we move to next property or not
        //                if (lastValue == price.Text)
        //                    continue;
        //                address.Zestimate=Convert.ToDouble(price.Text.Replace(",","").Replace("$",""));
        //                lastValue = price.Text;
        //                Thread.Sleep(2000);
        //            }
        //            catch (Exception)
        //            {

        //            }

        //        }
        //        webDriver.Close();
        //    }

        //    return model;
        //}
    }

    public class Datum
    {
        public int propertyId { get; set; }
        public int referenceNumber { get; set; }
        public DateTime salesDate { get; set; }
        public string plaintiff { get; set; }
        public string defendant { get; set; }
        public string propertyAddress { get; set; }
        public int eventTypeId { get; set; }
    }

    public class RootObject
    {
        public RootObject()
        {
            data = new List<Datum>();
        }
        public int draw { get; set; }
        public int recordsTotal { get; set; }
        public int recordsFiltered { get; set; }
        public List<Datum> data { get; set; }
    }


    ////////////////////////////////////////////////
    ///

    [XmlRoot(ElementName = "request")]
    public class Request
    {
        [XmlElement(ElementName = "address")]
        public string Address { get; set; }
        [XmlElement(ElementName = "citystatezip")]
        public string Citystatezip { get; set; }
    }

    [XmlRoot(ElementName = "message")]
    public class Message
    {
        [XmlElement(ElementName = "text")]
        public string Text { get; set; }
        [XmlElement(ElementName = "code")]
        public string Code { get; set; }
    }

    [XmlRoot(ElementName = "links")]
    public class Links
    {
        [XmlElement(ElementName = "homedetails")]
        public string Homedetails { get; set; }
        [XmlElement(ElementName = "graphsanddata")]
        public string Graphsanddata { get; set; }
        [XmlElement(ElementName = "mapthishome")]
        public string Mapthishome { get; set; }
        [XmlElement(ElementName = "comparables")]
        public string Comparables { get; set; }
        [XmlElement(ElementName = "overview")]
        public string Overview { get; set; }
        [XmlElement(ElementName = "forSaleByOwner")]
        public string ForSaleByOwner { get; set; }
        [XmlElement(ElementName = "forSale")]
        public string ForSale { get; set; }
    }

    [XmlRoot(ElementName = "address")]
    public class Address
    {
        [XmlElement(ElementName = "street")]
        public string Street { get; set; }
        [XmlElement(ElementName = "zipcode")]
        public string Zipcode { get; set; }
        [XmlElement(ElementName = "city")]
        public string City { get; set; }
        [XmlElement(ElementName = "state")]
        public string State { get; set; }
        [XmlElement(ElementName = "latitude")]
        public string Latitude { get; set; }
        [XmlElement(ElementName = "longitude")]
        public string Longitude { get; set; }
    }

    [XmlRoot(ElementName = "lastSoldPrice")]
    public class LastSoldPrice
    {
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "amount")]
    public class Amount
    {
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "oneWeekChange")]
    public class OneWeekChange
    {
        [XmlAttribute(AttributeName = "deprecated")]
        public string Deprecated { get; set; }
    }

    [XmlRoot(ElementName = "valueChange")]
    public class ValueChange
    {
        [XmlAttribute(AttributeName = "duration")]
        public string Duration { get; set; }
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "low")]
    public class Low
    {
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "high")]
    public class High
    {
        [XmlAttribute(AttributeName = "currency")]
        public string Currency { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "valuationRange")]
    public class ValuationRange
    {
        [XmlElement(ElementName = "low")]
        public Low Low { get; set; }
        [XmlElement(ElementName = "high")]
        public High High { get; set; }
    }

    [XmlRoot(ElementName = "zestimate")]
    public class Zestimate
    {
        [XmlElement(ElementName = "amount")]
        public Amount Amount { get; set; }
        [XmlElement(ElementName = "last-updated")]
        public string Lastupdated { get; set; }
        [XmlElement(ElementName = "oneWeekChange")]
        public OneWeekChange OneWeekChange { get; set; }
        [XmlElement(ElementName = "valueChange")]
        public ValueChange ValueChange { get; set; }
        [XmlElement(ElementName = "valuationRange")]
        public ValuationRange ValuationRange { get; set; }
        [XmlElement(ElementName = "percentile")]
        public string Percentile { get; set; }
    }

    [XmlRoot(ElementName = "region")]
    public class Region
    {
        [XmlElement(ElementName = "zindexValue")]
        public string ZindexValue { get; set; }
        [XmlElement(ElementName = "links")]
        public Links Links { get; set; }
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }
    }

    [XmlRoot(ElementName = "localRealEstate")]
    public class LocalRealEstate
    {
        [XmlElement(ElementName = "region")]
        public List<Region> Region { get; set; }
    }

    [XmlRoot(ElementName = "result")]
    public class Result
    {
        [XmlElement(ElementName = "zpid")]
        public string Zpid { get; set; }
        [XmlElement(ElementName = "links")]
        public Links Links { get; set; }
        [XmlElement(ElementName = "address")]
        public Address Address { get; set; }
        [XmlElement(ElementName = "FIPScounty")]
        public string FIPScounty { get; set; }
        [XmlElement(ElementName = "useCode")]
        public string UseCode { get; set; }
        [XmlElement(ElementName = "taxAssessmentYear")]
        public string TaxAssessmentYear { get; set; }
        [XmlElement(ElementName = "taxAssessment")]
        public string TaxAssessment { get; set; }
        [XmlElement(ElementName = "yearBuilt")]
        public string YearBuilt { get; set; }
        [XmlElement(ElementName = "lotSizeSqFt")]
        public string LotSizeSqFt { get; set; }
        [XmlElement(ElementName = "finishedSqFt")]
        public string FinishedSqFt { get; set; }
        [XmlElement(ElementName = "bathrooms")]
        public string Bathrooms { get; set; }
        [XmlElement(ElementName = "bedrooms")]
        public string Bedrooms { get; set; }
        [XmlElement(ElementName = "lastSoldDate")]
        public string LastSoldDate { get; set; }
        [XmlElement(ElementName = "lastSoldPrice")]
        public LastSoldPrice LastSoldPrice { get; set; }
        [XmlElement(ElementName = "zestimate")]
        public Zestimate Zestimate { get; set; }
        [XmlElement(ElementName = "localRealEstate")]
        public LocalRealEstate LocalRealEstate { get; set; }
    }

    [XmlRoot(ElementName = "results")]
    public class Results
    {
        [XmlElement(ElementName = "result")]
        public Result Result { get; set; }
    }

    [XmlRoot(ElementName = "response")]
    public class Response
    {
        [XmlElement(ElementName = "results")]
        public Results Results { get; set; }
    }

    [XmlRoot(ElementName = "SearchResults")]
    public class SearchResults
    {
        [XmlElement(ElementName = "request")]
        public Request Request { get; set; }
        [XmlElement(ElementName = "message")]
        public Message Message { get; set; }
        [XmlElement(ElementName = "response")]
        public Response Response { get; set; }
    }



}
