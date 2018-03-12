﻿// // --------------------------------------------------------------------------------------------------------------------
// // <copyright file="ItemPriceWithBadImplementationForEqualityAndUnicity.cs">
// //     Copyright 2016
// //           Thomas PIERRAIN (@tpierrain)    
// //     Licensed under the Apache License, Version 2.0 (the "License");
// //     you may not use this file except in compliance with the License.
// //     You may obtain a copy of the License at
// //         http://www.apache.org/licenses/LICENSE-2.0
// //     Unless required by applicable law or agreed to in writing, software
// //     distributed under the License is distributed on an "AS IS" BASIS,
// //     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// //     See the License for the specific language governing permissions and
// //     limitations under the License.b 
// // </copyright>
// // --------------------------------------------------------------------------------------------------------------------
namespace Value.Tests.Samples
{
    /// <summary>
    /// Price for an item. Bad Value Type implementation because doesn't override GetAllAttributesToBeUsedForEquality()
    /// </summary>
    public class ItemPriceWithBadImplementationForEqualityAndUnicity : Amount
    {
        public ItemPriceWithBadImplementationForEqualityAndUnicity(string itemName, decimal quantity, Currency currency) : base(quantity, currency)
        {
            this.ItemName = itemName;
        }

        public string ItemName { get; private set; }

        public override string ToString()
        {
            return $"{this.ItemName} - price: {this.Quantity} {this.Currency}.";
        }
    }
}