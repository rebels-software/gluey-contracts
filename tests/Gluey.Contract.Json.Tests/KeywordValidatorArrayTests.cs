// Copyright 2025 Rebels Software sp. z o.o.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Gluey.Contract;
using Gluey.Contract.Json;

namespace Gluey.Contract.Json.Tests;

[TestFixture]
public class KeywordValidatorArrayTests
{
    // ── GetItemSchema — items only ───────────────────────────────────────

    [Test]
    public void GetItemSchema_ItemsOnly_Index0_ReturnsItemsSchema()
    {
        var items = new SchemaNode("", type: SchemaType.String);

        SchemaNode? result = KeywordValidator.GetItemSchema(0, null, items);

        result.Should().BeSameAs(items);
    }

    [Test]
    public void GetItemSchema_ItemsOnly_Index5_ReturnsItemsSchema()
    {
        var items = new SchemaNode("", type: SchemaType.Number);

        SchemaNode? result = KeywordValidator.GetItemSchema(5, null, items);

        result.Should().BeSameAs(items);
    }

    // ── GetItemSchema — prefixItems only ─────────────────────────────────

    [Test]
    public void GetItemSchema_PrefixItemsOnly_Index0_ReturnsPrefixItems0()
    {
        var prefix0 = new SchemaNode("", type: SchemaType.String);
        var prefix1 = new SchemaNode("", type: SchemaType.Number);
        var prefixItems = new[] { prefix0, prefix1 };

        SchemaNode? result = KeywordValidator.GetItemSchema(0, prefixItems, null);

        result.Should().BeSameAs(prefix0);
    }

    [Test]
    public void GetItemSchema_PrefixItemsOnly_BeyondLength_ReturnsNull()
    {
        var prefix0 = new SchemaNode("", type: SchemaType.String);
        var prefixItems = new[] { prefix0 };

        SchemaNode? result = KeywordValidator.GetItemSchema(1, prefixItems, null);

        result.Should().BeNull();
    }

    // ── GetItemSchema — prefixItems + items ──────────────────────────────

    [Test]
    public void GetItemSchema_PrefixAndItems_WithinPrefixRange_ReturnsPrefixItem()
    {
        var prefix0 = new SchemaNode("", type: SchemaType.String);
        var prefix1 = new SchemaNode("", type: SchemaType.Integer);
        var items = new SchemaNode("", type: SchemaType.Boolean);
        var prefixItems = new[] { prefix0, prefix1 };

        SchemaNode? result = KeywordValidator.GetItemSchema(1, prefixItems, items);

        result.Should().BeSameAs(prefix1);
    }

    [Test]
    public void GetItemSchema_PrefixAndItems_BeyondPrefixRange_ReturnsItemsSchema()
    {
        var prefix0 = new SchemaNode("", type: SchemaType.String);
        var items = new SchemaNode("", type: SchemaType.Number);
        var prefixItems = new[] { prefix0 };

        SchemaNode? result = KeywordValidator.GetItemSchema(1, prefixItems, items);

        result.Should().BeSameAs(items);
    }

    // ── GetItemSchema — both null ────────────────────────────────────────

    [Test]
    public void GetItemSchema_BothNull_ReturnsNull()
    {
        SchemaNode? result = KeywordValidator.GetItemSchema(0, null, null);

        result.Should().BeNull();
    }

    // ── GetItemSchema — boundary cases ───────────────────────────────────

    [Test]
    public void GetItemSchema_PrefixLength3_Index2_ReturnsLastPrefixItem()
    {
        var prefix0 = new SchemaNode("", type: SchemaType.String);
        var prefix1 = new SchemaNode("", type: SchemaType.Number);
        var prefix2 = new SchemaNode("", type: SchemaType.Boolean);
        var items = new SchemaNode("", type: SchemaType.Null);
        var prefixItems = new[] { prefix0, prefix1, prefix2 };

        SchemaNode? result = KeywordValidator.GetItemSchema(2, prefixItems, items);

        result.Should().BeSameAs(prefix2);
    }

    [Test]
    public void GetItemSchema_PrefixLength3_Index3_ReturnsItemsSchema()
    {
        var prefix0 = new SchemaNode("", type: SchemaType.String);
        var prefix1 = new SchemaNode("", type: SchemaType.Number);
        var prefix2 = new SchemaNode("", type: SchemaType.Boolean);
        var items = new SchemaNode("", type: SchemaType.Null);
        var prefixItems = new[] { prefix0, prefix1, prefix2 };

        SchemaNode? result = KeywordValidator.GetItemSchema(3, prefixItems, items);

        result.Should().BeSameAs(items);
    }
}
