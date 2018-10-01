using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using HtmlAgilityPack;

namespace Skyrim_Alchemy_Utility {
    public class WebScraper {
        public static Dictionary<string, Effect> Effects = new Dictionary<string, Effect>();
        public static Dictionary<string, Ingredient> Ingredients = new Dictionary<string, Ingredient>();
        public static List<Property> Properties = new List<Property>();

        private const string XmlDb = "alchemy.xml";
        private string _appDataDir;

        public void LoadDb(bool rebuildDb = false) {

            _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyrimAlchemy");

            var dbFilePath = Path.Combine(_appDataDir, XmlDb);
            var serializer = new XmlSerializer(typeof(DB));

            var buildDbFromWeb = rebuildDb || !Directory.Exists(_appDataDir) || !File.Exists(dbFilePath);
            buildDbFromWeb = true;
            if (buildDbFromWeb) {
                Dictionary<string, Effect> tmpEffects = null;
                Effect[] effects;
                Ingredient[] ingredients;
                List<Property> tmpProps = null;

                Properties.Clear();
                effects = BuildEffects();
                Effects = effects.ToDictionary(ef => ef.ID, ef => ef);
                ingredients = BuildIngredients(Constants.UrlIngredientsMain).Concat(BuildIngredients(Constants.UrlIngredientsDragonborn)).ToArray();

                Ingredients = ingredients.ToDictionary(ing => ing.ID, ing => ing);

                if (!Directory.Exists(_appDataDir)) Directory.CreateDirectory(_appDataDir);
                if (File.Exists(dbFilePath)) {
                    File.Delete(dbFilePath);
                }

                var db = new DB { Effects = effects, Ingredients = ingredients, Properties = Properties.ToArray() };
                var ns = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
                var settings = new XmlWriterSettings() { Indent = true, OmitXmlDeclaration = true };
                XmlWriter writer = XmlWriter.Create(dbFilePath, settings);
                serializer.Serialize(writer, db, ns);
                writer.Close();

            }
            else {
                var reader = new StreamReader(dbFilePath);
                var db = (DB)serializer.Deserialize(reader);

                Effects = db.Effects.ToDictionary(ef => ef.ID, ef => ef);
                Ingredients = db.Ingredients.ToDictionary(ing => ing.ID, ing => ing);
                Properties = db.Properties.ToList();

                reader.Close();
            }
            EndUpdate:;
        }


        public Ingredient[] BuildIngredients(string url) {
            var web = new HtmlWeb();
            var doc = web.Load(url);

            var rows = doc.DocumentNode.SelectSingleNode("//table[@class='wikitable']").SelectNodes("tr");

            var ings = new List<Ingredient>();

            var newIng = true;
            Ingredient ing = null;
            foreach (var row in rows.Skip(1)) {
                var colIndex = 0;
                int defaultDlcCode;
                switch (url) {
                    default:
                    case Constants.UrlIngredientsMain: defaultDlcCode = 0; break;
                    case Constants.UrlIngredientsDragonborn: defaultDlcCode = 3; break;
                }
                if (newIng) ing = new Ingredient { DLC = defaultDlcCode };
                foreach (HtmlNode cell in row.SelectNodes("th|td")) {
                    var text = cell.InnerText;
                    if (newIng) {
                        switch (colIndex) {
                            case 0:
                                break;
                            case 1:
                                var parts = text.Split('\n');
                                ing.Name = parts[0];
                                if (ing.Name.Contains("DG")) {
                                    ing.Name = ing.Name.Replace("DG", "");
                                    ing.DLC = 1;
                                }
                                else if (ing.Name.Contains("HF")) {
                                    ing.Name = ing.Name.Replace("HF", "");
                                    ing.DLC = 2;
                                }
                                ing.ID = parts[1].ToUpper();
                                break;
                            case 2: ing.Description = text; break;
                        }
                    }
                    else {
                        switch (colIndex) {
                            case 0:
                            case 1:
                            case 2:
                            case 3:
                                if (text.Contains(";")) text = text.Split(';')[1];
                                var prop = new Property {
                                    IngID = ing.ID,
                                    EfID = Effects.Values.First(ef => text.Split(new[] { " (" }, StringSplitOptions.None)[0].Equals(ef.Name)).ID
                                };
                                Properties.Add(prop);
                                break;
                            case 4: ing.Value = Int32.Parse(text); break;
                            case 5: ing.Weight = Single.Parse(text); break;
                            case 6: // Merchant Avail.
                            case 7: // Garden
                            default:
                                break;
                        }
                    }
                    colIndex++;
                }
                newIng = !newIng;
                if (!ings.Any(ingr => ingr.ID.Equals(ing.ID))) ings.Add(ing);
            }
            return ings.ToArray();
        }
        public Effect[] BuildEffects() {
            var web = new HtmlWeb();
            var doc = web.Load(Constants.UrlEffects);

            var rows = doc.DocumentNode.SelectSingleNode("//table[@class='wikitable sortable']").SelectNodes("tr");
            var headings = (from HtmlNode cell in rows.First().SelectNodes("th|td")
                            select cell.InnerText).ToArray();

            var reNum = new Regex(@"^\d+");

            var efs = new List<Effect>();
            foreach (var row in rows.Skip(1)) {
                var colIndex = 0;
                var ef = new Effect();
                foreach (HtmlNode cell in row.SelectNodes("th|td")) {
                    var text = cell.InnerText;
                    switch (headings[colIndex]) {
                        case "Effect (ID)":
                            var _class = cell.Attributes["class"].Value;
                            ef.IsBeneficial = _class.Equals("EffectPos") ? 1 : 0;
                            var parts = text.Split(new[] { "\n(", ")" }, StringSplitOptions.None);
                            ef.Name = parts[0];
                            ef.ID = parts[1];
                            break;
                        case "Description":
                            ef.Description = text.Replace("&lt;", "<")
                                                 .Replace("&gt;", ">");
                            break;
                        case "Base_Cost": ef.Cost = Single.Parse(text); break;
                        case "Base_Mag":
                            ef.Mag = Int32.Parse(reNum.Match(text).Groups[0].Value);
                            ef.Description = ef.Description.Replace("<mag>", ef.Mag.ToString());
                            break;
                        case "Base_Dur":
                            ef.Dur = Int32.Parse(reNum.Match(text).Groups[0].Value);
                            ef.Description = ef.Description.Replace("<dur>", ef.Dur.ToString());
                            break;
                        case "": ef.Value = Int32.Parse(reNum.Match(text).Groups[0].Value); break;

                        case "Ingredients":
                        default:
                            break;
                    }
                    colIndex++;
                }
                efs.Add(ef);
            }
            return efs.ToArray();
        }

    }
}