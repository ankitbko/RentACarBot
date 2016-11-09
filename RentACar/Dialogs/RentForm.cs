using System;
using Microsoft.Bot.Builder.FormFlow;
using Chronic;
using Microsoft.Bot.Builder.FormFlow.Advanced;

namespace RentACar.Dialogs
{
    [Serializable]
    public class RentForm
    {
        private DateTime? _pickDate;
        private DateTime? _dropDate;

        [Prompt("Where is the pick up location?")]
        public string PickLocation { get; set; }

        [Prompt("Where is the drop location?")]
        public string DropLocation { get; set; }

        [Prompt("When would you pick up your vehicle?")]
        [Describe("Pick up Date and Time")]
        public string PickDateAndTime
        {
            get
            {
                return _pickDate?.ToString("dd MMMM 'at' h:mm tt");
            }
            set
            {
                DateTime dnt;
                DateTime.TryParse(value, out dnt);
                _pickDate = dnt;
            }
        }

        [Prompt("When will you return the vehicle?")]
        [Describe("Drop off Date and Time")]
        public string DropDateAndTime
        {
            get
            {
                return _dropDate?.ToString("dd MMMM 'at' h:mm tt");
            }
            set
            {
                DateTime dnt;
                DateTime.TryParse(value, out dnt);
                _dropDate = dnt;
            }
        }

        [Prompt("Which car would you like? {||}")]
        public Car Car { get; set; }

        public static  IForm<RentForm> BuildForm()
        {
            var parser = new Parser();
            return new FormBuilder<RentForm>()
                .Field(nameof(PickLocation))
                .Field(nameof(DropLocation), DropLocationActive)
                .Field(nameof(PickDateAndTime),
                validate: async (state, response) =>
                {
                    var value = (string)response;
                    var result = new ValidateResult() { IsValid = false, Feedback = "Invalid Pick up date and time" };
                    var pickDate = parser.Parse(value)?.Start ?? DateTime.MinValue;
                    if (DateTime.Compare(DateTime.Now, pickDate) <= 0)
                    {
                        result.IsValid = true;
                        result.Feedback = null;
                        result.Value = pickDate.ToString();
                    }
                    return result;
                })
                .Field(nameof(DropDateAndTime), DropDateTimeActive,
                validate: async (state, response) =>
                {
                    var value = (string)response;
                    var result = new ValidateResult() { IsValid = false, Feedback = "Invalid Drop off date and time" };
                    var dropDate = parser.Parse(value)?.Start ?? DateTime.MinValue;
                    var pickDate = state._pickDate ?? DateTime.MaxValue;
                    if (DateTime.Compare(pickDate, dropDate) < 0)
                    {
                        result.IsValid = true;
                        result.Feedback = null;
                        result.Value = dropDate.ToString();
                    }
                    return result;
                })
                .AddRemainingFields()
                .Build();
        }

        public static ActiveDelegate<RentForm> DropLocationActive => (state) => state.PickLocation != null;
        public static ActiveDelegate<RentForm> DropDateTimeActive => (state) => !string.IsNullOrWhiteSpace(state.PickDateAndTime);
    }

    public enum Car
    {
        FordFocus = 1,
        HondaAccord
    }
}