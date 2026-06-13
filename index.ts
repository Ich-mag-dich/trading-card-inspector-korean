const string_ko = Bun.file("./src/strings_ko.json");
const json = await string_ko.json();
const tables = json.tables;

interface enKo {
  en: string;
  ko: string;
}

function countEnglishVowels(str: string): number {
  const matches = str.match(/[aeiou]/gi);
  return matches ? matches.length : 0;
}

for (const value of Object.values(tables.CardTable_en) as enKo[]) {
  if (value.en.length > 20) {
    continue;
  }
  const vowelCount = countEnglishVowels(value.en);
  value.ko = `${value.ko} (${vowelCount})`;
}

await Bun.write("./src/strings_ko.json", JSON.stringify(json, null, 2));
console.log("Done.");
