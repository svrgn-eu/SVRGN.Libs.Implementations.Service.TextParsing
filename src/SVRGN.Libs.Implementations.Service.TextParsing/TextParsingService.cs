using SVRGN.Libs.Contracts.Service.Logging;
using SVRGN.Libs.Contracts.TextParsing;
using SVRGN.Libs.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVRGN.Libs.Implementations.Service.TextParsing
{
    public class TextParsingService : ITextParsingService
    {
        #region Properties

        private string[] conditionIdentifiers = new string[] { "if", "when" };
        private string[] conditionSplitters = new string[] { "then" };
        private string[] conditionPartSplitters = new string[] { "is", "and", "and is" };  //do not add "is not" as the "not" is important for the result then
        private string[] orderChoiceSeperators = new string[] { "from", "to", "based on" };  //which key words to use to seperate the order from the choice part
        private string[] primitiveOrderChoiceSeperators = new string[] { " " };  //which key words to use to seperate the order from the choice part in a primitive imperative sentence like "Randomize Everything"
        private string[] choiceLogicSeperators = new string[] { ":" };
        private string[] logicItemSplitters = new string[] { ";" };
        private string[] logicItemInternalSplitters = new string[] { "then" };
        private string[] comparisonOperators = new string[] { "=", "<", ">", "<=", ">=", "=>", "=<" };

        private Random random;

        private ILogService logService;

        #endregion Properties

        #region Construction

        public TextParsingService(ILogService LogService)
        {
            this.logService = LogService;

            this.random = new Random();
        }
        #endregion Construction

        #region Methods

        #region RandomizeFromChoiceList: Randomizes a result from a string with the input format "'(255/0/220), 20%', '(76/255/243), 40%', '(255/ 234/ 12), *'" or 'string1, 20%', 'string2, 40%', 'string4, *' or '(255/0/220)', '(76/255/243)', '(255/ 234/ 12)'"
        /// <summary>
        /// Randomizes a result from a string with the input format "'(255/0/220), 20%', '(76/255/243), 40%', '(255/ 234/ 12), *'" or 'string1, 20%', 'string2, 40%', 'string4, *' or '(255/0/220)', '(76/255/243)', '(255/ 234/ 12)'"
        /// </summary>
        /// <param name="Choices">the choices in the input format "'(255/0/220), 20%', '(76/255/243), 40%', '(255/ 234/ 12), *'" or 'string1, 20%', 'string2, 40%', 'string4, *' or '(255/0/220)', '(76/255/243)', '(255/ 234/ 12)'"</param>
        /// <returns>One of the strings in Choices with regards to the possible percent values</returns>
        public string RandomizeFromChoiceList(string Choices)
        {
            //TODO: generalize and move to base step / language understanding code part
            string result = string.Empty;

            //set up target structure which is Color - Probability
            List<KeyValuePair<string, float>> colorChoices = new List<KeyValuePair<string, float>>();

            string[] choices = Choices.Split(new string[] { "'," }, StringSplitOptions.None);
            int summedUpProbabilities = 0;
            foreach (string choice in choices)
            {
                string cleanedChoice = choice.Trim();
                cleanedChoice = cleanedChoice.Replace("'", "");
                string probabilityString = string.Empty;
                if (cleanedChoice.Contains("%") || cleanedChoice.Contains("*"))  // probability given for randomizing
                {
                    string[] choiceParts = cleanedChoice.Split(new string[] { "," }, StringSplitOptions.None);  // split after rgb values
                    probabilityString = choiceParts[1];
                    cleanedChoice = choiceParts[0];
                }
                //cleanedChoice = cleanedChoice.Replace("(", "");
                //cleanedChoice = cleanedChoice.Replace(")", "");
                cleanedChoice = cleanedChoice.Trim();

                float probability = 0f;
                if (probabilityString.Contains("%"))
                {
                    probabilityString = probabilityString.Replace("%", "");
                    int percentValue = 0;
                    if (!int.TryParse(probabilityString, out percentValue))
                    {

                        this.logService.Warning("TextParsingService", "RandomizeFromChoiceList", $"Could not convert a probability value from choice '{Choices}', affected part is '{probabilityString}'");
                    }
                    else
                    {
                        summedUpProbabilities += percentValue;
                        probability = percentValue / 100f;
                    }
                }
                else if (probabilityString.Contains("*"))
                {
                    // fill the rest
                    probability = (100 - summedUpProbabilities) / 100f;
                }

                colorChoices.Add(new KeyValuePair<string, float>(cleanedChoice, probability));
            }

            // next up: fill up probabilities where needed
            float probabilitiesCovered = 0;
            colorChoices.ForEach(x => probabilitiesCovered += x.Value);
            if (probabilitiesCovered < 1f)
            {
                // user input was less than 100% in total
                // calculate rest and split over the items without probability level
                float rest = 1f - probabilitiesCovered;
                int numberOfItemsToCover = colorChoices.Where(x => x.Value.Equals(0f)).Count();
                float restPerItem = rest / (float)numberOfItemsToCover;

                List<KeyValuePair<string, float>> tempChoices = new List<KeyValuePair<string, float>>();  // build up a new list in parallel as modifying the current one in a loop is just bad practise and does not work sufficiently

                foreach (KeyValuePair<string, float> entry in colorChoices)
                {
                    if (entry.Value.Equals(0f))
                    {
                        KeyValuePair<string, float> newEntry = new KeyValuePair<string, float>(entry.Key, restPerItem);
                        tempChoices.Add(newEntry);
                    }
                    else
                    {
                        tempChoices.Add(entry);
                    }
                }

                colorChoices = tempChoices;  // replace original list after building up the new one

            }
            else if (probabilitiesCovered > 1f)
            {
                // user input was more than 100% in total
                // wtf?
                // remove parts from each statement
                // user input was more than 100% in total
                // calculate rest and split over the items without probability level
                float tooMuch = 1f - probabilitiesCovered;
                tooMuch = Math.Abs(tooMuch);
                float tooMuchPerItem = tooMuch / (float)colorChoices.Count();

                List<KeyValuePair<string, float>> tempColorChoices = new List<KeyValuePair<string, float>>();  // build up a new list in parallel as modifying the current one in a loop is just bad practise and does not work sufficiently

                foreach (KeyValuePair<string, float> entry in colorChoices)
                {
                    KeyValuePair<string, float> newEntry = new KeyValuePair<string, float>(entry.Key, entry.Value - tooMuchPerItem);
                    tempColorChoices.Add(newEntry);
                }

                colorChoices = tempColorChoices;  // replace original list after building up the new one
            }

            //now randomize based on colorChoices List
            int randomNumber = this.random.Next(0, 100);
            float randomNumberInFloat = (float)randomNumber / 100f;
            float accumulatedProbability = 0;
            for (int i = 0; i < colorChoices.Count; i++)
            {
                accumulatedProbability += colorChoices[i].Value;
                if (randomNumberInFloat <= accumulatedProbability)
                {
                    //return color
                    result = colorChoices[i].Key;
                    break;
                }
            }

            return result;
        }
        #endregion RandomizeFromChoiceList

        #region HasCondition
        public bool HasCondition(string Text)
        {
            bool result = false;

            string editedText = Text.ToLower();
            foreach (string conditionWord in this.conditionIdentifiers)
            {
                if (editedText.StartsWith(conditionWord.ToLower()))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }
        #endregion HasCondition

        #region ExtractConditions
        public List<string> ExtractConditions(string Input, out string Rest)
        {
            List<string> result = new List<string>();

            //string cleanedInput = Input.ToLower();
            string cleanedInput = Input;
            string[] newConditionParts = cleanedInput.Split(this.conditionSplitters, StringSplitOptions.None);
            string newCondition = newConditionParts[0];
            Rest = newConditionParts[1].Trim();
            newCondition = newCondition.Substring(2);  // get rid of the 'if'
            newCondition = newCondition.Replace(", ", " ");
            newCondition = newCondition.Replace(",", " ");
            newCondition = newCondition.Trim();  //trim empty space at the beginning and end

            // check for linked conditions, if no and exists then the only condition will be used
            string[] linkedConditionParts = newCondition.Split(new string[] { "and" }, StringSplitOptions.None);
            foreach (string linkedCondition in linkedConditionParts)
            {
                result.Add(linkedCondition.Trim());
            }

            return result;
        }
        #endregion ExtractConditions

        #region ExtractOrder
        public string ExtractOrder(string Input)
        {
            string result = string.Empty;
            string[] parts = Input.Split(this.orderChoiceSeperators, StringSplitOptions.None);
            result = parts[0].Trim();
            return result;
        }
        #endregion ExtractOrder

        #region ExtractOrderFromPrimitiveText
        public string ExtractOrderFromPrimitiveText(string Input)
        {
            string result = string.Empty;
            string[] parts = Input.Split(this.primitiveOrderChoiceSeperators, StringSplitOptions.None);
            result = parts[0].Trim();
            return result;
        }
        #endregion ExtractOrderFromPrimitiveText

        #region ExtractChoice
        public string ExtractChoice(string Input)
        {
            string result = string.Empty;
            string[] parts = Input.Split(this.orderChoiceSeperators, StringSplitOptions.None);
            string[] choiceLogicParts = parts[1].Trim().Split(this.choiceLogicSeperators, StringSplitOptions.None);
            result = choiceLogicParts[0].Trim();
            return result;
        }
        #endregion ExtractChoice

        #region ExtractChoiceFromPrimitiveText
        public string ExtractChoiceFromPrimitiveText(string Input)
        {
            string result = string.Empty;
            string[] parts = Input.Split(this.primitiveOrderChoiceSeperators, StringSplitOptions.None);
            result = parts[1].Trim();
            return result;
        }
        #endregion ExtractChoiceFromPrimitiveText

        #region ExtractLogic
        public string ExtractLogic(string Input)
        {
            string result = string.Empty;
            string[] parts = Input.Split(this.orderChoiceSeperators, StringSplitOptions.None);
            string[] choiceLogicParts = parts[1].Trim().Split(this.choiceLogicSeperators, StringSplitOptions.None);
            if (choiceLogicParts.Length > 1)
            {
                //only extract a logic if there is one
                result = choiceLogicParts[1].Trim();
            }
            else
            {
                result = string.Empty;
            }
            return result;
        }
        #endregion ExtractLogic

        #region SplitCondition
        public List<string> SplitCondition(string Condition)
        {
            List<string> result = new List<string>();

            string[] parts = Condition.Split(this.conditionPartSplitters, StringSplitOptions.None);
            if (parts.Length > 0)
            {
                foreach (string part in parts)
                {
                    string newLine = part.Trim();
                    if (!string.IsNullOrEmpty(newLine))  // only add not empty parts
                    {
                        result.Add(newLine);  //do the cleaning step
                    }
                }
            }

            return result;
        }
        #endregion SplitCondition

        #region SplitLogicItems: will split an input text of the format "when 'Body Shape' = Hourglass then 'Hips'=A,C, D; when 'Body Shape' = Apple then 'Hips'=B, E(25%); " into individual lines like "'Body Shape' = Hourglass|'Hips'=A,C, D"
        /// <summary>
        /// will split an input text of the format "when 'Body Shape' = Hourglass then 'Hips'=A,C, D; when 'Body Shape' = Apple then 'Hips'=B, E(25%); " into individual lines like "'Body Shape' = Hourglass|'Hips'=A,C, D"
        /// </summary>
        /// <param name="Input">the input text</param>
        /// <returns>a list of pipe.seperated strings containing the prerequisite and the consquences for further processing</returns>
        public IList<string> SplitLogicItems(string Input)
        {
            List<string> result = default;

            string[] parts = Input.Split(this.logicItemSplitters, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                result = new List<string>();
                foreach (string line in parts)
                {
                    string[] logicParts = line.Split(this.logicItemInternalSplitters, StringSplitOptions.RemoveEmptyEntries);
                    int requiredLength = 2;
                    if (logicParts.Length.Equals(requiredLength))
                    {
                        string newLine = string.Empty;

                        newLine += $"{logicParts[0].Replace("When", "").Replace("when", "").Trim()}|{logicParts[1].Trim()}";

                        result.Add(newLine);
                    }
                    else
                    {
                        this.logService.Warning("TextParsingService", "SplitLogicItems", $"Splitting of the logic item line '{line}' not successful, unequal than '{requiredLength}' parts were splitted out.");
                    }
                }
            }

            return result;
        }
        #endregion SplitLogicItems

        #region SplitSimpleCondition
        public IList<string> SplitSimpleCondition(string Input)
        {
            IList<string> result = new List<string>();

            string comparison = Input.GetContainingString(this.comparisonOperators);
            string[] parts = Input.Split(this.comparisonOperators, StringSplitOptions.RemoveEmptyEntries);

            if (!string.IsNullOrEmpty(comparison) && parts.Length.Equals(2))
            {
                result.Add(parts[0].Trim());
                result.Add(comparison);
                result.Add(parts[1].Trim());
            }

            return result;
        }
        #endregion SplitSimpleCondition

        #endregion Methods

        #region Events

        #endregion Events
    }
}
