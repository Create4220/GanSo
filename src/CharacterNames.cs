using System;
using System.IO;

namespace GanSo {
 public static class CharacterNames {
  // Vocalist order supplied by the user; full-cast spelling checked against artwork.
  static readonly string[] Vocalists={"Kasumi","Ran","Aya","Yukina","Kokoro","Mashiro","Layer","Tomori","Uika","Arale"};
  static readonly string[] FullCast={
   "Kasumi","Arisa","Rimi","Sāya","Tae","Ran","Moca","Himari","Tomoe","Tsugumi",
   "Tomori","Anon","Rāna","Soyo","Taki","Kokoro","Kaoru","Hagumi","Kanon","Michelle",
   "Yukina","Sayo","Lisa","Ako","Rinko","Mashiro","Tōko","Nanami","Tsukushi","Rui",
   "Aya","Hina","Chisato","Maya","Eve","Layer","Lock","Masking","Pareo","Chu²",
   "Uika","Mutsumi","Umiri","Nyamu","Sakiko","Arale","Nonoka","Ritsu","Miyako","Yuno"
  };
  public static string FromPath(string path){
   string file=Path.GetFileNameWithoutExtension(path??"");int index;
   if(file.Length<2||!int.TryParse(file.Substring(0,2),out index))return "";
   return index>=1&&index<=10?Vocalists[index-1]:index>=11&&index<=60?FullCast[index-11]:"";
  }
 }
}
