using System;
using System.IO;

namespace GanSo {
 public static class CharacterNames {
  // Match the names printed on the full-cast artwork, in source-image order 11–60.
  static readonly string[] FullCast={
   "KASUMI","ARISA","RIMI","SAYA","TAE","RAN","MOCA","HIMARI","TOMOE","TSUGUMI",
   "TOMORI","ANON","RANA","SOYO","TAKI","KOKORO","KAORU","HAGUMI","KANON","MICHELLE",
   "YUKINA","SAYO","LISA","AKO","RINKO","MASHIRO","TOKO","NANAMI","TSUKUSHI","RUI",
   "AYA","HINA","CHISATO","MAYA","EVE","LAYER","LOCK","MASKING","PAREO","CHU²",
   "UIKA","MUTSUMI","UMIRI","NYAMU","SAKIKO","ARALE","NONOKA","RITSU","MIYAKO","YUNO"
  };
  public static string FromPath(string path){
   string file=Path.GetFileNameWithoutExtension(path??"");int index;
   if(file.Length<2||!int.TryParse(file.Substring(0,2),out index))return "";
   return index>=11&&index<=60?FullCast[index-11]:"";
  }
 }
}
