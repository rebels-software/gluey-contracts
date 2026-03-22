// Copyright 2026 Rebels Software sp. z o.o.
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

using Gluey.Contract.Binary.Schema;

namespace Gluey.Contract.Binary.Tests;

[TestFixture]
internal sealed class EndiannessResolutionTests
{
    private static Dictionary<string, BinaryContractNode> SingleField(string? fieldEndianness = null)
    {
        return new Dictionary<string, BinaryContractNode>
        {
            ["field"] = new BinaryContractNode
            {
                Name = "field", DependsOn = null, Type = "uint16", Size = 2,
                Endianness = fieldEndianness
            },
        };
    }

    [Test]
    public void Resolve_ContractLittleNoFieldOverride_ResolvedEndiannessIsLittle()
    {
        var ordered = BinaryChainResolver.Resolve(SingleField(), "little");

        ordered[0].ResolvedEndianness.Should().Be(0); // 0 = little
    }

    [Test]
    public void Resolve_ContractBigNoFieldOverride_ResolvedEndiannessIsBig()
    {
        var ordered = BinaryChainResolver.Resolve(SingleField(), "big");

        ordered[0].ResolvedEndianness.Should().Be(1); // 1 = big
    }

    [Test]
    public void Resolve_ContractLittleFieldOverrideBig_ResolvedEndiannessIsBig()
    {
        var ordered = BinaryChainResolver.Resolve(SingleField("big"), "little");

        ordered[0].ResolvedEndianness.Should().Be(1); // field override wins
    }

    [Test]
    public void Resolve_NoContractEndiannessNoFieldOverride_DefaultsToLittle()
    {
        var ordered = BinaryChainResolver.Resolve(SingleField(), null);

        ordered[0].ResolvedEndianness.Should().Be(0); // default = little
    }

    [Test]
    public void Resolve_ContractBigFieldOverrideLittle_ResolvedEndiannessIsLittle()
    {
        var ordered = BinaryChainResolver.Resolve(SingleField("little"), "big");

        ordered[0].ResolvedEndianness.Should().Be(0); // field override wins
    }

    [Test]
    public void Resolve_StructSubFields_GetResolvedEndianness()
    {
        var structFields = new[]
        {
            new BinaryContractNode { Name = "code", DependsOn = null, Type = "uint16", Size = 2 },
            new BinaryContractNode { Name = "ts", DependsOn = "code", Type = "uint16", Size = 2, Endianness = "big" },
        };

        var fields = new Dictionary<string, BinaryContractNode>
        {
            ["arr"] = new BinaryContractNode
            {
                Name = "arr", DependsOn = null, Type = "array", Size = 0,
                Count = 1, ArrayElement = new ArrayElementInfo("struct", 4, structFields),
                StructFields = structFields,
            },
        };

        var ordered = BinaryChainResolver.Resolve(fields, "little");

        var sf = ordered[0].ArrayElement!.StructFields!;
        sf[0].ResolvedEndianness.Should().Be(0); // inherits contract "little"
        sf[1].ResolvedEndianness.Should().Be(1); // overrides to "big"
    }
}
