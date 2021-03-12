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
            Cart = new CartData(cartInfo.PortalId, "", cartInfo.ItemID.ToString());
            SettingsData = new SettingsLimpet(Cart);
        }

        /// <summary>
        /// Get shipping cost, the Cart.PurchaseInfo class is updated with the amounts 
        /// </summary>
        public void UpdateShippingCost()
        {
            ParamCmd = "chronopost_relais";

            if ((SettingsData.FreeShippingLimit > 0 && (SettingsData.FreeShippingLimit <= SettingsData.SubTotal) || SettingsData.TotalWeight == 0))
            {
                // return zero if we have invalid data
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingcost", "0");
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingcostTVA", "0");
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingdealercost", "0");
                Cart.PurchaseInfo.SetXmlProperty("genxml/chronopostmessage", "");
            }
            else
            {
                // get soap xml from resx
                var soapxmlfilename = Utils.MapPath("/DesktopModules/NBright/OS_Chronopost/soapquickcost.xml");
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

                var nbi = GetSoapReturn(soapxml, "https://www.chronopost.fr/quickcost-cxf/QuickcostServiceWS");

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(nbi.XMLData);
                var nsMgr = new XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
                nsMgr.AddNamespace("ns1", "http://cxf.quickcost.soap.chronopost.fr/");
                Double shippingcostTCC = 0;
                Double shippingcostTVA = 0;
                var shippingmsg = "";

                var shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:quickCostResponse/return/amountTTC", nsMgr);
                if (shippingnod != null && Utils.IsNumeric(shippingnod.InnerText)) shippingcostTCC = Convert.ToDouble(shippingnod.InnerText, CultureInfo.GetCultureInfo("en-US"));
                shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:quickCostResponse/return/amountTVA", nsMgr);
                if (shippingnod != null && Utils.IsNumeric(shippingnod.InnerText)) shippingcostTVA = Convert.ToDouble(shippingnod.InnerText, CultureInfo.GetCultureInfo("en-US"));
                shippingnod = doc.SelectSingleNode("/soap:Envelope/soap:Body/ns1:quickCostResponse/return/errorMessage", nsMgr);
                if (shippingnod != null) shippingmsg = shippingnod.InnerText;

                var shippingdealercost = shippingcostTCC;
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingcost", shippingcostTCC);
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingcostTVA", shippingcostTVA);
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingdealercost", shippingdealercost);
                Cart.PurchaseInfo.SetXmlProperty("genxml/chronopostmessage", shippingmsg);

                /// TEST
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingcost", 999);
                Cart.PurchaseInfo.SetXmlPropertyDouble("genxml/shippingdealercost", 999);
            }
        }

        public List<NBrightInfo> GetRelais()
        {
            // get soap xml from resx
            var soapxmlfilename = Utils.MapPath("/DesktopModules/NBright/OS_Chronopost/soappointlist.xml");
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
            var soapxmlfilename = Utils.MapPath("/DesktopModules/NBright/OS_Chronopost/soaplabel.xml");
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
                var pickuppoint = Cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/hidden/pickuppointaddr");
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


                soapxml = soapxml.Replace("{country}", Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country"));
                soapxml = soapxml.Replace("{countrytext}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country/@selectedtext")));
                if (!Utils.IsEmail(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/email"))) Cart.PurchaseInfo.SetXmlProperty("genxml/billaddress/genxml/textbox/email", Cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/textbox/cartemailaddress"));
                soapxml = soapxml.Replace("{email}", Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/email"));
                soapxml = soapxml.Replace("{postalcode}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/postalcode")));

                soapxml = soapxml.Replace("{firstname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/firstname")));
                soapxml = soapxml.Replace("{lastname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/lastname")));
                soapxml = soapxml.Replace("{telephone}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/telephone")));


            }
            else
            {
                switch (SettingsData.ShippingOption)
                {
                    case "1":
                        soapxml = soapxml.Replace("{countrytext}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country/@selectedtext")));
                        if (!Utils.IsEmail(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/email")))
                            Cart.PurchaseInfo.SetXmlProperty("genxml/billaddress/genxml/textbox/email", Cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/textbox/cartemailaddress"));
                        soapxml = soapxml.Replace("{recfirstname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/firstname")));
                        soapxml = soapxml.Replace("{reclastname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/lastname")));
                        foreach (var s in Cart.PurchaseInfo.ToDictionary("genxml/billaddress/"))
                        {
                            soapxml = soapxml.Replace("{" + s.Key + "}", Utils.StripAccents(s.Value));
                        }
                        break;
                    case "2":
                        soapxml = soapxml.Replace("{countrytext}",
                            Cart.PurchaseInfo.GetXmlProperty("genxml/shipaddress/genxml/dropdownlist/country/@selectedtext"));
                        if (!Utils.IsEmail(Cart.PurchaseInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/email")))
                            Cart.PurchaseInfo.SetXmlProperty("genxml/shipaddress/genxml/textbox/email", Cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/textbox/cartemailaddress"));
                        soapxml = soapxml.Replace("{recfirstname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/firstname")));
                        soapxml = soapxml.Replace("{reclastname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/lastname")));
                        foreach (var s in Cart.PurchaseInfo.ToDictionary("genxml/shipaddress/"))
                        {
                            soapxml = soapxml.Replace("{" + s.Key + "}", Utils.StripAccents(s.Value));
                        }
                        break;
                    default:
                        soapxml = soapxml.Replace("{recfirstname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/firstname")));
                        soapxml = soapxml.Replace("{reclastname}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/lastname")));
                        foreach (var s in Cart.PurchaseInfo.ToDictionary("genxml/billaddress/"))
                        {
                            soapxml = soapxml.Replace("{" + s.Key + "}", "");
                        }
                        break;
                }
            }

            soapxml = soapxml.Replace("{recipientPreAlert}", "0");
            soapxml = soapxml.Replace("{ordernumber}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/ordernumber")));
            DateTime shippingdate = DateTime.Today;
            if (Utils.IsDate(Cart.PurchaseInfo.GetXmlProperty("genxml/textbox/shippingdate"))) shippingdate = Convert.ToDateTime(Cart.PurchaseInfo.GetXmlProperty("genxml/textbox/shippingdate"));
            soapxml = soapxml.Replace("{shipdate}", shippingdate.ToString("yyyy-MM-dd") + "Y12:00:00.000Z");

            soapxml = soapxml.Replace("{mode}", SettingsData.PrintMode);

            if (SettingsData.ProductType == "D")
                soapxml = soapxml.Replace("{objecttype}", "DOC");
            else
                soapxml = soapxml.Replace("{objecttype}", "MAR");

            if (SelectedProductCode == "86")
                soapxml = soapxml.Replace("{recipientref}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/radiobuttonlist/chronopostrelais")));
            else
                soapxml = soapxml.Replace("{recipientref}", Utils.StripAccents(Cart.PurchaseInfo.GetXmlProperty("genxml/textbox/trackingcode")));

            // string any unmatch tokens
            var aryTokens = Utils.ParseTemplateText(soapxml, "{", "}");
            var lp = 1;
            soapxml = "";
            foreach (var s in aryTokens)
            {
                if (lp % 2 != 0) soapxml += Utils.StripAccents(s);
                lp += 1;
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
            foreach (var p in Cart.GetCartItemList())
            {
                var productData = new ProductData(p.ItemID, Utils.GetCurrentCulture());
                if (productData.HasProperty(propertyref)) return true;
            }
            return false;
        }

        private NBrightInfo GetSoapReturn(String soapxml, String url)
        {
            if (StoreSettings.Current.DebugMode)
            {
                var nbi = new NBrightInfo();
                nbi.XMLData = soapxml;
                nbi.XMLDoc.Save(PortalSettings.Current.HomeDirectoryMapPath + "\\debug_chronopostsoap.xml");
            }

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

        public string ParamCmd { set { Cart.PurchaseInfo.SetXmlProperty("genxml/chronopostparamcmd", value.ToString()); } get { return Cart.PurchaseInfo.GetXmlProperty("genxml/chronopostparamcmd"); } }
        public string SelectedProductCode { set { Cart.PurchaseInfo.SetXmlProperty("genxml/chronopostproductcode", value.ToString()); } get { return Cart.PurchaseInfo.GetXmlProperty("genxml/chronopostproductcode"); } }
        public string ShippingKey { get { return "chronopost2"; } }
        public CartData Cart { set; get; }
        public SettingsLimpet SettingsData { set; get; }        
    }
}
