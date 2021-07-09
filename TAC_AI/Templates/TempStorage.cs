﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TAC_AI.Templates
{
    public class BaseTemplate
    {
        public string techName = "!!!!null!!!!";
        public FactionSubTypes faction = FactionSubTypes.NULL;
        public List<BasePurpose> purposes;
        public BaseTerrain terrain = BaseTerrain.Land;
        public int startingFunds = 5000;
        public string savedTech = "{\"blockType\":125,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}";
    }
    class TempStorage
    {
        public const string NotAvailableTech = "{\"blockType\":125,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":-4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":-4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":-4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":4.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":7.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":7.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":8.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":8.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":9.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":10.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":4.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":10.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":7.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":8.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":10.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":10.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":10.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":10.0,\"z\":10.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":10.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":11.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":12.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":12.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":11.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":12.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":12.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":12.0},\"CacheRot\":0}|{\"blockType\":361,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":11.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":14.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":15.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":15.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":16.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":16.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":18.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":19.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":7.0,\"z\":19.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":19.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":19.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":18.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":5.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":6.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":17.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":8.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":9.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":10.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":11.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":12.0,\"z\":13.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":12.0,\"z\":12.0},\"CacheRot\":0}|{\"blockType\":374,\"CachePos\":{\"x\":0.0,\"y\":12.0,\"z\":11.0},\"CacheRot\":0}";

        public static Dictionary<SpawnBaseTypes, BaseTemplate> techBasesAll = new Dictionary<SpawnBaseTypes, BaseTemplate>
        {
            { SpawnBaseTypes.NotAvail, new BaseTemplate {
                techName = "error on load",
                faction = FactionSubTypes.NULL,
                purposes = new List<BasePurpose>(),
                startingFunds = -1,
                savedTech = NotAvailableTech
            } },
            { SpawnBaseTypes.GSOSeller, new BaseTemplate {
                techName = "GSO Seller",
                faction = FactionSubTypes.GSO,
                purposes = new List<BasePurpose>{BasePurpose.Harvesting},
                startingFunds = 10000,
                savedTech = "{\"blockType\":125,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":8,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":162,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":162,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":162,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":162,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":111,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":3}|{\"blockType\":111,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":111,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":1}|{\"blockType\":111,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":2}|{\"blockType\":111,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":3}|{\"blockType\":111,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":2}|{\"blockType\":111,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":1}|{\"blockType\":111,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":21,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":0.0},\"CacheRot\":0}"
            } },
            { SpawnBaseTypes.GSOMidBase, new BaseTemplate {
                techName = "GSO Furlough Base",
                faction = FactionSubTypes.GSO,
                purposes = new List<BasePurpose>{BasePurpose.Harvesting},
                startingFunds = 75000,
                savedTech = "{\"blockType\":125,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":8,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":162,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":162,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":-1.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":1.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":1.0,\"y\":1.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":116,\"CachePos\":{\"x\":-1.0,\"y\":1.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":135,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":135,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":135,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":135,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":135,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":135,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":4,\"CachePos\":{\"x\":0.0,\"y\":3.0,\"z\":0.0},\"CacheRot\":9}|{\"blockType\":117,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":117,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":378,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":3}|{\"blockType\":378,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":1}|{\"blockType\":105,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-5.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":-5.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":5.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":105,\"CachePos\":{\"x\":5.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":125,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":125,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":125,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":125,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":4,\"CachePos\":{\"x\":3.0,\"y\":2.0,\"z\":3.0},\"CacheRot\":16}|{\"blockType\":4,\"CachePos\":{\"x\":-3.0,\"y\":2.0,\"z\":3.0},\"CacheRot\":16}|{\"blockType\":4,\"CachePos\":{\"x\":-3.0,\"y\":2.0,\"z\":-3.0},\"CacheRot\":16}|{\"blockType\":4,\"CachePos\":{\"x\":3.0,\"y\":2.0,\"z\":-3.0},\"CacheRot\":16}|{\"blockType\":7,\"CachePos\":{\"x\":0.0,\"y\":4.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":7,\"CachePos\":{\"x\":-3.0,\"y\":3.0,\"z\":3.0},\"CacheRot\":3}|{\"blockType\":7,\"CachePos\":{\"x\":-3.0,\"y\":3.0,\"z\":-3.0},\"CacheRot\":3}|{\"blockType\":7,\"CachePos\":{\"x\":3.0,\"y\":3.0,\"z\":-3.0},\"CacheRot\":1}|{\"blockType\":7,\"CachePos\":{\"x\":3.0,\"y\":3.0,\"z\":3.0},\"CacheRot\":1}|{\"blockType\":111,\"CachePos\":{\"x\":-6.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":1}|{\"blockType\":111,\"CachePos\":{\"x\":-5.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":2}|{\"blockType\":111,\"CachePos\":{\"x\":-6.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":1}|{\"blockType\":111,\"CachePos\":{\"x\":-5.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":111,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":0}|{\"blockType\":111,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":0}|{\"blockType\":111,\"CachePos\":{\"x\":5.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":111,\"CachePos\":{\"x\":6.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":3}|{\"blockType\":111,\"CachePos\":{\"x\":6.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":3}|{\"blockType\":111,\"CachePos\":{\"x\":5.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":2}|{\"blockType\":111,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":4.0},\"CacheRot\":2}|{\"blockType\":111,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":4.0},\"CacheRot\":2}|{\"blockType\":622,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":20}|{\"blockType\":622,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":20}|{\"blockType\":8,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":1}|{\"blockType\":8,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":3}|{\"blockType\":91,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":1}|{\"blockType\":91,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":1}|{\"blockType\":28,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":0}|{\"blockType\":28,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":0}|{\"blockType\":30,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":3}|{\"blockType\":30,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":-6.0},\"CacheRot\":1}|{\"blockType\":30,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":-6.0},\"CacheRot\":1}|{\"blockType\":30,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":3}|{\"blockType\":7,\"CachePos\":{\"x\":3.0,\"y\":1.0,\"z\":-5.0},\"CacheRot\":3}|{\"blockType\":7,\"CachePos\":{\"x\":-3.0,\"y\":1.0,\"z\":-5.0},\"CacheRot\":1}|{\"blockType\":115,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":115,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":3.0},\"CacheRot\":0}|{\"blockType\":115,\"CachePos\":{\"x\":-4.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":115,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":0}|{\"blockType\":16,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":1}|{\"blockType\":16,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":3}"
            } },
            { SpawnBaseTypes.GSOAIMinerProduction, new BaseTemplate {  //WIP
                techName = "GSO Production",
                faction = FactionSubTypes.GSO,
                purposes = new List<BasePurpose>{BasePurpose.TechProduction},
                startingFunds = 125000,
                savedTech = NotAvailableTech
            } },
            { SpawnBaseTypes.HEAircraftGarrison, new BaseTemplate {  //WIP
                techName = "Hawkeye Aircraft Garrison",
                faction = FactionSubTypes.HE,
                purposes = new List<BasePurpose>{BasePurpose.TechProduction},
                startingFunds = 125000,
                savedTech = NotAvailableTech
            } },
            { SpawnBaseTypes.GSOMilitaryBase, new BaseTemplate {  //WIP
                techName = "GSO Diplomacy Outpost",
                faction = FactionSubTypes.HE,
                purposes = new List<BasePurpose>
                {
                    BasePurpose.TechProduction,
                    BasePurpose.Headquarters
                },
                startingFunds = 175000,
                savedTech = NotAvailableTech
            } },
            { SpawnBaseTypes.GCHeadquarters, new BaseTemplate {  //WIP
                techName = "GeoCorp Overlord Complex",
                faction = FactionSubTypes.HE,
                purposes = new List<BasePurpose>
                {
                    BasePurpose.TechProduction,
                    BasePurpose.Harvesting,
                    BasePurpose.Headquarters
                },
                startingFunds = 175000,
                savedTech = NotAvailableTech
            } },
            { SpawnBaseTypes.HECommandCentre, new BaseTemplate {  //WIP
                techName = "Hawkeye Command Station",
                faction = FactionSubTypes.HE,
                purposes = new List<BasePurpose>
                {
                    BasePurpose.TechProduction,
                    BasePurpose.Harvesting,
                    BasePurpose.Headquarters
                },
                startingFunds = 175000,
                savedTech = NotAvailableTech
            } },
            { SpawnBaseTypes.TACInvaderAttract, new BaseTemplate {
                techName = "TAC InvaderAttract",
                faction = FactionSubTypes.NULL,
                terrain = BaseTerrain.Space,
                purposes = new List<BasePurpose>{ BasePurpose.NotABase, BasePurpose.NoAutoSearch },// not a base lol
                startingFunds = -1,
                savedTech = "{\"blockType\":584320,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":584321,\"CachePos\":{\"x\":4.0,\"y\":4.0,\"z\":6.0},\"CacheRot\":0}|{\"blockType\":584322,\"CachePos\":{\"x\":3.0,\"y\":4.0,\"z\":3.0},\"CacheRot\":8}|{\"blockType\":584322,\"CachePos\":{\"x\":5.0,\"y\":4.0,\"z\":3.0},\"CacheRot\":12}|{\"blockType\":584324,\"CachePos\":{\"x\":4.0,\"y\":5.0,\"z\":5.0},\"CacheRot\":0}|{\"blockType\":584332,\"CachePos\":{\"x\":4.0,\"y\":3.0,\"z\":5.0},\"CacheRot\":4}|{\"blockType\":584323,\"CachePos\":{\"x\":3.0,\"y\":5.0,\"z\":4.0},\"CacheRot\":0}|{\"blockType\":584323,\"CachePos\":{\"x\":5.0,\"y\":3.0,\"z\":4.0},\"CacheRot\":4}|{\"blockType\":584318,\"CachePos\":{\"x\":4.0,\"y\":4.0,\"z\":3.0},\"CacheRot\":4}"
            } },
            { SpawnBaseTypes.TACInvaderAttract2, new BaseTemplate {
                techName = "TAC InvaderAttract2",
                faction = FactionSubTypes.NULL,
                purposes = new List<BasePurpose>{ BasePurpose.NotABase, BasePurpose.NoAutoSearch },// not a base lol
                startingFunds = -1,
                savedTech = "{\"blockType\":584200,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":584239,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":584425,\"CachePos\":{\"x\":0.0,\"y\":1.0,\"z\":-1.0},\"CacheRot\":22}|{\"blockType\":584215,\"CachePos\":{\"x\":-2.0,\"y\":-1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":584215,\"CachePos\":{\"x\":-2.0,\"y\":-1.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":584215,\"CachePos\":{\"x\":2.0,\"y\":-1.0,\"z\":0.0},\"CacheRot\":0}|{\"blockType\":584215,\"CachePos\":{\"x\":2.0,\"y\":-1.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":584215,\"CachePos\":{\"x\":0.0,\"y\":-1.0,\"z\":-4.0},\"CacheRot\":0}|{\"blockType\":584395,\"CachePos\":{\"x\":3.0,\"y\":1.0,\"z\":-1.0},\"CacheRot\":19}|{\"blockType\":584395,\"CachePos\":{\"x\":-2.0,\"y\":1.0,\"z\":-1.0},\"CacheRot\":16}|{\"blockType\":584395,\"CachePos\":{\"x\":-2.0,\"y\":1.0,\"z\":-2.0},\"CacheRot\":21}|{\"blockType\":584395,\"CachePos\":{\"x\":3.0,\"y\":1.0,\"z\":-2.0},\"CacheRot\":22}|{\"blockType\":584207,\"CachePos\":{\"x\":-1.0,\"y\":1.0,\"z\":-2.0},\"CacheRot\":8}|{\"blockType\":584207,\"CachePos\":{\"x\":2.0,\"y\":1.0,\"z\":-2.0},\"CacheRot\":8}|{\"blockType\":584353,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":-5.0},\"CacheRot\":16}|{\"blockType\":584240,\"CachePos\":{\"x\":2.0,\"y\":1.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":584240,\"CachePos\":{\"x\":-1.0,\"y\":1.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":584261,\"CachePos\":{\"x\":3.0,\"y\":1.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":584261,\"CachePos\":{\"x\":-2.0,\"y\":1.0,\"z\":1.0},\"CacheRot\":0}|{\"blockType\":584304,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":584304,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":0}|{\"blockType\":584307,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":18}|{\"blockType\":584306,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":2.0},\"CacheRot\":18}|{\"blockType\":584305,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-1.0},\"CacheRot\":3}|{\"blockType\":584305,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":1.0},\"CacheRot\":1}|{\"blockType\":584315,\"CachePos\":{\"x\":4.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":15}|{\"blockType\":584315,\"CachePos\":{\"x\":-3.0,\"y\":0.0,\"z\":-2.0},\"CacheRot\":11}|{\"blockType\":584275,\"CachePos\":{\"x\":0.0,\"y\":2.0,\"z\":-2.0},\"CacheRot\":0}|{\"blockType\":584303,\"CachePos\":{\"x\":2.0,\"y\":2.0,\"z\":-1.0},\"CacheRot\":0}|{\"blockType\":584303,\"CachePos\":{\"x\":-1.0,\"y\":2.0,\"z\":-1.0},\"CacheRot\":3}|{\"blockType\":584393,\"CachePos\":{\"x\":2.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":10}|{\"blockType\":584393,\"CachePos\":{\"x\":-1.0,\"y\":1.0,\"z\":0.0},\"CacheRot\":10}|{\"blockType\":584291,\"CachePos\":{\"x\":3.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":2}|{\"blockType\":584269,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":23}|{\"blockType\":584268,\"CachePos\":{\"x\":-1.0,\"y\":2.0,\"z\":-3.0},\"CacheRot\":10}|{\"blockType\":584269,\"CachePos\":{\"x\":2.0,\"y\":1.0,\"z\":-3.0},\"CacheRot\":21}|{\"blockType\":584268,\"CachePos\":{\"x\":2.0,\"y\":2.0,\"z\":-3.0},\"CacheRot\":14}|{\"blockType\":584302,\"CachePos\":{\"x\":-1.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":10}|{\"blockType\":584302,\"CachePos\":{\"x\":2.0,\"y\":0.0,\"z\":-4.0},\"CacheRot\":14}|{\"blockType\":584291,\"CachePos\":{\"x\":0.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":2}|{\"blockType\":584291,\"CachePos\":{\"x\":1.0,\"y\":0.0,\"z\":-5.0},\"CacheRot\":2}|{\"blockType\":584291,\"CachePos\":{\"x\":-2.0,\"y\":0.0,\"z\":-3.0},\"CacheRot\":2}"
            } },

        };
    }
}
