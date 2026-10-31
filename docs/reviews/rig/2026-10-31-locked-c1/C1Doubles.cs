using System;
using System.Linq;
using System.Collections.Generic;
namespace Entities {public enum MarketingTypeName{SmallInternet,MediumInternet,LargeInternet,SmallBillboard,MediumBillboard,LargeBillboard,None}public class MarketingCampaign{public MarketingTypeName marketingTypeName;public bool enabled=true;public Address agencyAddress=new Address();}}
public class Address{public string streetName="rig agency";public int streetNumber=7;}
public class Promotion{public int trafficIndex=20,marketing=30,total=50;}
public class DoubleReg{public List<Entities.MarketingCampaign> marketingCampaigns=new List<Entities.MarketingCampaign>();public Promotion promotion=new Promotion();public int Money{get{throw new Exception("money getter invoked");}set{throw new Exception("money setter invoked");}}public void UpdatePromotion(){throw new Exception("helper invoked");}}
public class ChangedReg{public object[] marketingCampaigns=new object[0];public string promotion="changed";}
public class ChangedEnumReg{public List<Entities.MarketingCampaign> marketingCampaigns=new List<Entities.MarketingCampaign>{new ChangedCampaign()};public Promotion promotion=new Promotion();}
public class ChangedCampaign:Entities.MarketingCampaign{public new string marketingTypeName="changed";}
public static class DoubleFactory{public static object Create(string kind){if(kind=="missing")return new object();if(kind=="changed")return new ChangedReg();if(kind=="changed-enum")return new ChangedEnumReg();var r=new DoubleReg();int count=kind=="six"?6:kind=="duplicate"?2:1;for(int i=0;i<count;i++)r.marketingCampaigns.Add(new Entities.MarketingCampaign{marketingTypeName=(Entities.MarketingTypeName)(kind=="none"?6:kind=="unsupported"?999:kind=="duplicate"?0:i),enabled=kind!="disabled"});return r;}}
