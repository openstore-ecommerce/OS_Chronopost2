using DotNetNuke.Entities.Portals;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;
using OS_Chronopost2.Components;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Nevoweb.OS_Chronopost2.Components
{
    public class ChronopostLimpet
    {
        public ChronopostLimpet(NBrightInfo cartInfo)
        {
            CartInfo = cartInfo;
            ChronofreshOnly = CheckCartForProperty("chronofresh");
            //DO NOT LOAD THE CART AGAIN, the weight of the cart is calculated but not saved to the DB. Use the cartInfo to get data.
            SettingsData = new SettingsLimpet(CartInfo);
        }

        /// <summary>
        /// Get shipping cost, the CartInfo class is updated with the amounts 
        /// </summary>
        public void UpdateShippingCost()
        {
            ParamCmd = "chronopost_relais";

            if (ChronofreshOnly)
            {
                SelectedProductCode = "2R";
                CartInfo.SetXmlProperty("genxml/pickuppointref", "");
                CartInfo.SetXmlProperty("genxml/pickuppointaddr", "");
            }
            else
            {
                if (SelectedProductCode == "")
                {
                    if (ProductCodeList().Count > 0)
                    {
                        SelectedProductCode = ProductCodeList().First().Key;
                    }
                }
                if (SelectedProductCode != "86")  // if not Relais, clear previous value.
                {
                    CartInfo.SetXmlProperty("genxml/pickuppointref", "");
                    CartInfo.SetXmlProperty("genxml/pickuppointaddr", "");
                }
            }


            if ((SettingsData.FreeShippingLimit > 0 && (SettingsData.FreeShippingLimit <= SettingsData.SubTotal)))
            {
                // return zero if we have invalid data
                CartInfo.SetXmlPropertyDouble("genxml/shippingcost", "0");
                CartInfo.SetXmlPropertyDouble("genxml/shippingcostTVA", "0");
                CartInfo.SetXmlPropertyDouble("genxml/shippingdealercost", "0");
                CartInfo.SetXmlProperty("genxml/chronopostmessage", "");
            }
            else
            {
                Double shippingcostTCC = SettingsData.Info.GetXmlPropertyDouble("genxml/textbox/overwritepricettc" + SelectedProductCode.ToLower());
                Double shippingcostTVA = SettingsData.Info.GetXmlPropertyDouble("genxml/textbox/overwritepricetva" + SelectedProductCode.ToLower());
                var shippingmsg = "";

                if (shippingcostTCC == 0)
                {
                    // get soap xml from resx
                    var soapxmlfilename = Utils.MapPath("/DesktopModules/NBright/OS_Chronopost2/soapquickcost.xml");
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(soapxmlfilename);
                    var soapxml = xmlDoc.OuterXml;
                    // replace the tokens in the soap XML strucutre.
                    soapxml = soapxml.Replace("{accountnumber}", SettingsData.AccountNumber);
                    soapxml = soapxml.Replace("{password}", SettingsData.Password);
                    soapxml = soapxml.Replace("{depcode}", Utils.StripAccents(SettingsData.DistributionPostCode));
                    soapxml = soapxml.Replace("{arrcode}", SettingsData.ArrivalPostCode);
                    soapxml = soapxml.Replace("{weight}", SettingsData.TotalWeight.ToString(CultureInfo.GetCultureInfo("en-US")));
                    soapxml = soapxml.Replace("{productcode}", SelectedProductCode);
                    soapxml = soapxml.Replace("{type}", SettingsData.ProductType);

                    if (StoreSettings.Current.DebugMode)
                    {
                        var nbi2 = new NBrightInfo();
                        nbi2.XMLData = soapxml;
                        nbi2.XMLDoc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug_chronopostsoap_" + SelectedProductCode + ".xml");
                    }

                    var nbi = GetSoapReturn(soapxml, "https://www.chronopost.fr/quickcost-cxf/QuickcostServiceWS");

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(nbi.XMLData);
                    var nsMgr = new XmlNamespaceManager(doc.NameTable);
                    nsMgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
                    nsMgr.AddNamespace("ns1", "http://cxf.quickcost.soap.chronopost.fr/");

                    if (StoreSettings.Current.DebugMode)
                    {
                        doc.Save(StoreSettings.Current.FolderTempMapPath + "\\chronopostreturn_" + SelectedProductCode + ".xml");
                    }

                    var shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:quickCostResponse/return/amountTTC", nsMgr);
                    if (shippingnod != null && Utils.IsNumeric(shippingnod.InnerText)) shippingcostTCC = Convert.ToDouble(shippingnod.InnerText, CultureInfo.GetCultureInfo("en-US"));
                    shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:quickCostResponse/return/amountTVA", nsMgr);
                    if (shippingnod != null && Utils.IsNumeric(shippingnod.InnerText)) shippingcostTVA = Convert.ToDouble(shippingnod.InnerText, CultureInfo.GetCultureInfo("en-US"));
                    shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:quickCostResponse/return/errorMessage", nsMgr);
                    if (shippingnod != null) shippingmsg = shippingnod.InnerText;

                }

                var shippingdealercost = shippingcostTCC;
                CartInfo.SetXmlPropertyDouble("genxml/shippingcost", shippingcostTCC);
                CartInfo.SetXmlPropertyDouble("genxml/shippingcostTVA", shippingcostTVA);
                CartInfo.SetXmlPropertyDouble("genxml/shippingdealercost", shippingdealercost);
                CartInfo.SetXmlProperty("genxml/chronopostmessage", shippingmsg);

                /// TEST
                //CartInfo.SetXmlPropertyDouble("genxml/shippingcost", 999);
                //CartInfo.SetXmlPropertyDouble("genxml/shippingdealercost", 999);
            }
        }
        public void UpdateRelais(string encodedRealis)
        {
            var relaisData = NBrightBuyUtils.DeCode(encodedRealis);
            if (relaisData != "")
            {
                CartInfo.RemoveXmlNode("genxml/return");
                CartInfo.AddXmlNode(relaisData, "return", "genxml");
                CartInfo.SetXmlProperty("genxml/pickuppointref", CartInfo.GetXmlProperty("genxml/return/identifiantchronopostpointa2pas"));
                CartInfo.SetXmlProperty("genxml/pickuppointaddr", CartInfo.GetXmlProperty("genxml/return/nomenseigne") + ", " + CartInfo.GetXmlProperty("genxml/return/adresse1") + ", " + CartInfo.GetXmlProperty("genxml/return/localite"));
            }
        }
        public List<NBrightInfo> GetRelais()
        {
            // get soap xml from resx
            var soapxmlfilename = Utils.MapPath("/DesktopModules/NBright/OS_Chronopost2/soappointlist.xml");
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(soapxmlfilename);
            var soapxml = xmlDoc.OuterXml;
            // replace the tokens in the soap XML strucutre.
            soapxml = soapxml.Replace("{codeProduit}", SelectedProductCode);
            soapxml = soapxml.Replace("{codePostal}", Utils.StripAccents(SettingsData.ArrivalPostCode));
            var pickupdate = DateTime.Now.AddDays(SettingsData.LeadDays);
            if (pickupdate.DayOfWeek == DayOfWeek.Saturday || pickupdate.DayOfWeek == DayOfWeek.Sunday) pickupdate = pickupdate.AddDays(2);
            soapxml = soapxml.Replace("{date}", pickupdate.ToString("dd/MM/yyyy"));

            var nbi = GetSoapReturn(soapxml, "https://www.chronopost.fr/recherchebt-ws-cxf/PointRelaisServiceWS");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(nbi.XMLData);
            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            nsMgr.AddNamespace("ns1", "http://cxf.rechercheBt.soap.chronopost.fr/");

            if (StoreSettings.Current.DebugMode) doc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug_chronopostquickcostrtn.xml");

            // build list of points
            var relaisDicList = new List<NBrightInfo>();
            var nodList = doc.SelectNodes("/soap:Envelope/soap:Body/ns1:rechercheBtParCodeproduitEtCodepostalEtDateResponse/*", nsMgr);
            if (nodList != null)
            {
                var lp = 1;
                foreach (XmlNode nod in nodList)
                {
                    var nbirelais = new NBrightInfo(true);
                    nbirelais.XMLData = nod.OuterXml.ToLower();
                    nbirelais.GUIDKey = nbirelais.GetXmlProperty("return/identifiantChronopostPointA2PAS");
                    relaisDicList.Add(nbirelais);
                    if (StoreSettings.Current.DebugMode) nbirelais.XMLDoc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug" + lp + "_identifiantChronopostPointA2PAS.xml");
                    lp += 1;
                }
            }

            return relaisDicList;
        }

        public string GetDeliveryLabelUrl()
        {
            var rtnUrl = "https://www.chronopost.fr/shipping-cxf/getReservedSkybill?reservationNumber=";

            // get soap xml from resx
            var soapxmlfilename = Utils.MapPath("/DesktopModules/NBright/OS_Chronopost2/soaplabel.xml");
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(soapxmlfilename);
            var soapxml = xmlDoc.OuterXml;


            // replace the tokens in the soap XML strucutre.
            soapxml = soapxml.Replace("{accountNumber}", SettingsData.AccountNumber);
            soapxml = soapxml.Replace("{password}", SettingsData.Password);
            soapxml = soapxml.Replace("{weight}", SettingsData.TotalWeight.ToString("F"));

            soapxml = soapxml.Replace("{productcode}", SelectedProductCode);

            soapxml = soapxml.Replace("{shipperCountry}", Utils.StripAccents(SettingsData.DistributionCountryCode));
            //soapxml = soapxml.Replace("{shipperCountryName}", Utils.StripAccents(SettingsData.DistributionCountryName));


            // Chronopost only support 1 email address, so make sure we only take 1 from support email field.
            var supportemails = StoreSettings.Current.SettingsInfo.GetXmlProperty("genxml/textbox/supportemail");
            if (supportemails.Contains(','))
            {
                supportemails = supportemails.Split(',')[0].Trim();
            }
            soapxml = soapxml.Replace("{supportemail}", Utils.StripAccents(supportemails));


            foreach (var s in StoreSettings.Current.Settings())
            {
                soapxml = soapxml.Replace("{" + s.Key + "}", s.Value);
            }

            if (SelectedProductCode == "86")
            {
                // Is a relais, so force delivery address to the pickup point.
                var pickuppoint = CartInfo.GetXmlProperty("genxml/extrainfo/genxml/hidden/pickuppointaddr");
                var pickupary = pickuppoint.Split(',');
                var pickuppoint1 = "";
                var pickuppoint2 = "";

                if (pickupary.Count() >= 2)
                {
                    pickuppoint1 = pickupary[0] + "," + pickupary[1];
                    var lp1 = 0;
                    foreach (var p in pickupary)
                    {
                        if (lp1 > 1)
                        {
                            pickuppoint2 += p + ",";
                        }
                        lp1 += 1;
                    }
                    pickuppoint2 = pickuppoint2.TrimEnd(',');
                }
                else
                    pickuppoint1 = pickuppoint;

                soapxml = soapxml.Replace("{unit}", Utils.StripAccents(pickuppoint1));
                soapxml = soapxml.Replace("{street}", Utils.StripAccents(pickuppoint2));


                soapxml = soapxml.Replace("{country}", CartInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country"));
                soapxml = soapxml.Replace("{countrytext}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country/@selectedtext")));
                if (!Utils.IsEmail(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/email"))) CartInfo.SetXmlProperty("genxml/billaddress/genxml/textbox/email", CartInfo.GetXmlProperty("genxml/extrainfo/genxml/textbox/cartemailaddress"));
                soapxml = soapxml.Replace("{email}", CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/email"));
                soapxml = soapxml.Replace("{postalcode}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/postalcode")));

                soapxml = soapxml.Replace("{firstname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/firstname")));
                soapxml = soapxml.Replace("{lastname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/lastname")));
                soapxml = soapxml.Replace("{telephone}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/telephone")));


            }
            else
            {
                switch (SettingsData.ShippingOption)
                {
                    case "1":
                        soapxml = soapxml.Replace("{countrytext}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country/@selectedtext")));
                        if (!Utils.IsEmail(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/email")))
                            CartInfo.SetXmlProperty("genxml/billaddress/genxml/textbox/email", CartInfo.GetXmlProperty("genxml/extrainfo/genxml/textbox/cartemailaddress"));
                        soapxml = soapxml.Replace("{recfirstname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/firstname")));
                        soapxml = soapxml.Replace("{reclastname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/lastname")));
                        foreach (var s in CartInfo.ToDictionary("genxml/billaddress/"))
                        {
                            soapxml = soapxml.Replace("{" + s.Key + "}", Utils.StripAccents(s.Value));
                        }
                        break;
                    case "2":
                        soapxml = soapxml.Replace("{countrytext}",
                            CartInfo.GetXmlProperty("genxml/shipaddress/genxml/dropdownlist/country/@selectedtext"));
                        if (!Utils.IsEmail(CartInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/email")))
                            CartInfo.SetXmlProperty("genxml/shipaddress/genxml/textbox/email", CartInfo.GetXmlProperty("genxml/extrainfo/genxml/textbox/cartemailaddress"));
                        soapxml = soapxml.Replace("{recfirstname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/firstname")));
                        soapxml = soapxml.Replace("{reclastname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/lastname")));
                        foreach (var s in CartInfo.ToDictionary("genxml/shipaddress/"))
                        {
                            soapxml = soapxml.Replace("{" + s.Key + "}", Utils.StripAccents(s.Value));
                        }
                        break;
                    default:
                        soapxml = soapxml.Replace("{recfirstname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/firstname")));
                        soapxml = soapxml.Replace("{reclastname}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/lastname")));
                        foreach (var s in CartInfo.ToDictionary("genxml/billaddress/"))
                        {
                            soapxml = soapxml.Replace("{" + s.Key + "}", "");
                        }
                        break;
                }
            }

            soapxml = soapxml.Replace("{recipientPreAlert}", "0");
            soapxml = soapxml.Replace("{ordernumber}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/ordernumber")));
            DateTime shippingdate = DateTime.Today;
            if (Utils.IsDate(CartInfo.GetXmlProperty("genxml/textbox/shippingdate"))) shippingdate = Convert.ToDateTime(CartInfo.GetXmlProperty("genxml/textbox/shippingdate"));
            soapxml = soapxml.Replace("{shipdate}", shippingdate.ToString("yyyy-MM-dd") + "Y12:00:00.000Z");

            soapxml = soapxml.Replace("{mode}", SettingsData.PrintMode);

            if (SettingsData.ProductType == "D")
                soapxml = soapxml.Replace("{objecttype}", "DOC");
            else
                soapxml = soapxml.Replace("{objecttype}", "MAR");

            if (SelectedProductCode == "86")
                soapxml = soapxml.Replace("{recipientref}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/extrainfo/genxml/radiobuttonlist/chronopostrelais")));
            else
                soapxml = soapxml.Replace("{recipientref}", Utils.StripAccents(CartInfo.GetXmlProperty("genxml/textbox/trackingcode")));

            // string any unmatch tokens
            var aryTokens = Utils.ParseTemplateText(soapxml, "{", "}");
            var lp = 1;
            soapxml = "";
            foreach (var s in aryTokens)
            {
                if (lp % 2 != 0) soapxml += Utils.StripAccents(s);
                lp += 1;
            }

            if (StoreSettings.Current.DebugMode)
            {
                var nbi2 = new NBrightInfo();
                nbi2.XMLData = soapxml;
                nbi2.XMLDoc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug_chronopostlabelsoap_" + SelectedProductCode + ".xml");
            }

            var nbi = GetSoapReturn(soapxml, "https://www.chronopost.fr/shipping-cxf/ShippingServiceWS");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(nbi.XMLData);
            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            nsMgr.AddNamespace("ns1", "http://cxf.shipping.soap.chronopost.fr/");

            var resvnumber = "";
            var shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:shippingWithReservationAndESDWithRefClientResponse/return/reservationNumber", nsMgr);
            if (shippingnod != null && Utils.IsNumeric(shippingnod.InnerText)) resvnumber = shippingnod.InnerText;

            if (resvnumber == "")
            {
                doc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug_chronoposterr.xml");
                return "";
            }

            rtnUrl += resvnumber;

            return rtnUrl;
        }

        private bool CheckCartForProperty(string propertyref)
        {
            var xmlNodeList = CartInfo.XMLDoc.SelectNodes("genxml/items/*");
            if (xmlNodeList != null)
            {
                foreach (XmlNode carNod in xmlNodeList)
                {
                    var newInfo = new NBrightInfo { XMLData = carNod.OuterXml };
                    var productId = newInfo.GetXmlPropertyInt("genxml/productid");
                    var productData = new ProductData(productId, Utils.GetCurrentCulture());
                    if (productData.HasProperty(propertyref)) return true;
                }
            }
            return false;
        }

        private NBrightInfo GetSoapReturn(String soapxml, String url)
        {
            using (var client = new WebClient())
            {
                // the Content-Type needs to be set to XML
                client.Headers.Add("Content-Type", "text/xml;charset=utf-8");
                // The SOAPAction header indicates which method you would like to invoke
                // and could be seen in the WSDL: <soap:operation soapAction="..." /> element
                client.Headers.Add("SOAPAction", "");
                var response = client.UploadString(url, soapxml);
                var nbi = new NBrightInfo();
                nbi.XMLData = response;

                if (StoreSettings.Current.DebugMode) nbi.XMLDoc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug_chronopostresponse.xml");

                return nbi;
            }
        }
        public Dictionary<string, string> ProductCodeList()
        {
            return SettingsData.ProductCodeList();
        }

        public int UpdateCartInfo()
        {
            var objCtrl = new NBrightBuyController();
            return objCtrl.Update(CartInfo);
        }


        public string ParamCmd { set { CartInfo.SetXmlProperty("genxml/chronopostparamcmd", value.ToString()); } get { return CartInfo.GetXmlProperty("genxml/chronopostparamcmd"); } }
        public string SelectedProductCode { set { CartInfo.SetXmlProperty("genxml/chronopostproductcode", value.ToString()); } get { return CartInfo.GetXmlProperty("genxml/chronopostproductcode"); } }
        public string ShippingKey { get { return "chronopost2"; } }
        public bool ChronofreshOnly { set; get; }
        public SettingsLimpet SettingsData { set; get; }
        public NBrightInfo CartInfo { set; get; }
        
    }
}
