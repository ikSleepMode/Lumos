using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public static class LicenseCrypto {
 public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
 public static string Request(string fingerprint){return "LUMOS1-"+fingerprint+"-"+Hash("Lumos/request/1/"+fingerprint).Substring(0,8);}
 public static string Fingerprint(string request){var p=(request??"").Trim().ToUpperInvariant().Split('-');if(p.Length!=3||p[0]!="LUMOS1"||!System.Text.RegularExpressions.Regex.IsMatch(p[1],"^[A-F0-9]{64}$")||Request(p[1])!=(request??"").Trim().ToUpperInvariant())throw new FormatException("Неверный код устройства. Скопируйте его целиком.");return p[1];}
 public static string Encode(byte[] bytes){return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
 public static byte[] Decode(string value){value=value.Replace('-','+').Replace('_','/');return Convert.FromBase64String(value.PadRight((value.Length+3)/4*4,'='));}
 public static RSACryptoServiceProvider Rsa(){var rsa=new RSACryptoServiceProvider(3072,new CspParameters{ProviderType=24});rsa.PersistKeyInCsp=false;return rsa;}
 public static string Issue(string request,DateTime expiresUtc,DateTime nowUtc,string privateXml){
  if(expiresUtc<=nowUtc)throw new ArgumentException("Срок должен быть в будущем.");string payload="LUMOS1|"+Fingerprint(request)+"|"+nowUtc.Ticks.ToString(CultureInfo.InvariantCulture)+"|"+expiresUtc.Ticks.ToString(CultureInfo.InvariantCulture)+"|"+Guid.NewGuid().ToString("N");byte[] bytes=Encoding.UTF8.GetBytes(payload);using(var rsa=Rsa()){rsa.FromXmlString(privateXml);return Encode(bytes)+"."+Encode(rsa.SignData(bytes,"SHA256"));}
 }
 public static bool Verify(string token,string fingerprint,DateTime nowUtc,string publicXml,out DateTime expiry,out string error){
  expiry=DateTime.MinValue;error="Неверный код активации.";
  try{if(String.IsNullOrWhiteSpace(token)||token.Length>4096)return false;var parts=token.Trim().Split('.');if(parts.Length!=2)return false;byte[] bytes=Decode(parts[0]);using(var rsa=Rsa()){rsa.FromXmlString(publicXml);if(!rsa.VerifyData(bytes,"SHA256",Decode(parts[1])))return false;}
   var p=Encoding.UTF8.GetString(bytes).Split('|');if(p.Length!=5||p[0]!="LUMOS1")return false;if(p[1]!=fingerprint){error="Код выдан для другого компьютера.";return false;}
   var issued=new DateTime(Int64.Parse(p[2],CultureInfo.InvariantCulture),DateTimeKind.Utc);expiry=new DateTime(Int64.Parse(p[3],CultureInfo.InvariantCulture),DateTimeKind.Utc);if(expiry<=issued)return false;
   if(nowUtc.Ticks+TimeSpan.FromMinutes(5).Ticks<issued.Ticks){error="Проверьте дату и время Windows.";return false;}if(nowUtc>=expiry){error="Срок активации истёк. Запросите новый код.";return false;}error=null;return true;
  }catch(FormatException){return false;}catch(CryptographicException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
 }
}
