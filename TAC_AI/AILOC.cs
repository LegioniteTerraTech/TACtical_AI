using System.Collections.Generic;
using TerraTechETCUtil;

namespace TAC_AI
{
    internal class AILOC
    {

        internal static LocExtStringMod UnknownUnnamed = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "unknown" },
            { LocalisationEnums.Languages.Japanese, "未知"},
        });


        // -----------------------------------------------------------------------

        // -----------------------------------------------------------------------

        internal static LocExtStringMod AutoDisabled = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Self-Driving Off" },
            { LocalisationEnums.Languages.Japanese, "自動運転オフ"},
        });
        internal static LocExtStringMod AutoEnabled = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Self-Driving On" },
            { LocalisationEnums.Languages.Japanese, "自動運転オン"},
        });
        internal static LocExtStringMod CamFollowDisabled = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Follow Cam Off" },
            { LocalisationEnums.Languages.Japanese, "通常のカメラ"},
        });
        internal static LocExtStringMod CamFollowEnabled = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Follow Cam ON" },
            { LocalisationEnums.Languages.Japanese, "追跡カメラ"},
        });



        internal static LocExtStringMod NoAI = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "No AI Modules" },
            { LocalisationEnums.Languages.Japanese, "AIモジュールなし"},
        });
        internal static LocExtStringMod AIOff = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "AI Idle (Off)" },
            { LocalisationEnums.Languages.Japanese, "自動運転オフ"},
        });


        // -----------------------------------------------------------------------

        // -----------------------------------------------------------------------

        internal static LocExtStringMod AIProcessing = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Loading..." },
            { LocalisationEnums.Languages.Japanese, "読み込み中..."},
        });
        internal static LocExtStringMod AIArrived = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "At Destination" },
            { LocalisationEnums.Languages.Japanese, "目的地に到着しました"},
        });
        internal static LocExtStringMod AIAnchoredDefRetreat = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Holding the line!" },
            { LocalisationEnums.Languages.Japanese, "地盤を維持する"},
        });
        internal static LocExtStringMod AIRetreat = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Retreat!" },
            { LocalisationEnums.Languages.Japanese, "後退!"},
        });
        internal static LocExtStringMod AIStationary = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Stationary" },
            { LocalisationEnums.Languages.Japanese, "保持位置"},
        });
        internal static LocExtStringMod AIStationaryBase = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Stationary base" },
            { LocalisationEnums.Languages.Japanese, "固定ベース"},
        });

        // -----------------------------------------------------------------------

        // -----------------------------------------------------------------------
        internal static LocExtStringMod Destination = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "destination " },
            { LocalisationEnums.Languages.Japanese, "行き先"},
        });
        internal static LocExtStringMod Player = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "player " },
            { LocalisationEnums.Languages.Japanese, "プレーヤー"},
        });
        internal static LocExtStringMod Ally = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "ally " },
            { LocalisationEnums.Languages.Japanese, "味方"},
        });
        internal static LocExtStringMod Request = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "request " },
            { LocalisationEnums.Languages.Japanese, "要求"},
        });
        internal static LocExtStringMod Base = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "base " },
            { LocalisationEnums.Languages.Japanese, "ベース"},
        });
        internal static LocExtStringMod Enemy = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "enemy " },
            { LocalisationEnums.Languages.Japanese, "敵"},
        });
        internal static LocExtStringMod Target = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "target " },
            { LocalisationEnums.Languages.Japanese, "ターゲット"},
        });
        internal static LocExtStringMod Obst = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "obstruction " },
            { LocalisationEnums.Languages.Japanese, "障害物"},
        });
        internal static LocExtStringMod Energy = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "energy " },
            { LocalisationEnums.Languages.Japanese, "電気"},
        });
        internal static LocExtStringMod Cargo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "cargo " },
            { LocalisationEnums.Languages.Japanese, "貨物"},
        });


        internal static LocExtStringMod Fighting = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Fighting " },
            { LocalisationEnums.Languages.Japanese, "攻撃："},
        });
        internal static LocExtStringMod CombatOperator = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Combat - " },
            { LocalisationEnums.Languages.Japanese, "戦闘 - "},
        });
        internal static LocExtStringMod SearchFor = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Searching for " },
            { LocalisationEnums.Languages.Japanese, "探しています:"},
        });
        internal static LocExtStringMod Protect = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Protecting " },
            { LocalisationEnums.Languages.Japanese, "保護を適用する:"},
        });
        internal static LocExtStringMod Collect = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Collecting " },
            { LocalisationEnums.Languages.Japanese, "取る:"},
        });
        internal static LocExtStringMod Giving = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Giving " },
            { LocalisationEnums.Languages.Japanese, "与える:"},
        });
        internal static LocExtStringMod Mimic = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Mimicing " },
            { LocalisationEnums.Languages.Japanese, "ˈ真似する:"},
        });
        internal static LocExtStringMod Remove = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Removing " },
            { LocalisationEnums.Languages.Japanese, "削除する:"},
        });
        internal static LocExtStringMod Code = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Code ["},
            { LocalisationEnums.Languages.Japanese, "ダイブコード["},
        });


        internal static LocExtStringMod FaceTowards = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"turn to face "},
            { LocalisationEnums.Languages.Japanese, "顔を向ける:"},
        });
        internal static LocExtStringMod FaceAwayFrom = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"turn to face from "},
            { LocalisationEnums.Languages.Japanese, "から顔を向ける:"},
        });
        internal static LocExtStringMod HideFrom = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"hide from "},
            { LocalisationEnums.Languages.Japanese, "から隠す:"},
        });


        internal static LocExtStringMod Gen_MoveTo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Moving to "},
            { LocalisationEnums.Languages.Japanese, "によって移動する:"},
        });
        internal static LocExtStringMod Gnd_MoveTo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Driving to "},
            { LocalisationEnums.Languages.Japanese, "に運転して:"},
        });
        internal static LocExtStringMod Fly_MoveTo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Flying to "},
            { LocalisationEnums.Languages.Japanese, "に飛んでいます:"},
        });
        internal static LocExtStringMod Sea_MoveTo = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Sailing to "},
            { LocalisationEnums.Languages.Japanese, "に航海する:"},
        });


        internal static LocExtStringMod Gen_MoveFrom = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Moving from "},
            { LocalisationEnums.Languages.Japanese, "からの移動:"},
        });
        internal static LocExtStringMod Gnd_MoveFrom = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Driving from "},
            { LocalisationEnums.Languages.Japanese, "から運転する:"},
        });
        internal static LocExtStringMod Fly_MoveFrom = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Flying from "},
            { LocalisationEnums.Languages.Japanese, "からの飛行:"},
        });
        internal static LocExtStringMod Sea_MoveFrom = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Sailing from "},
            { LocalisationEnums.Languages.Japanese, "から出航:"},
        });


        // -----------------------------------------------------------------------

        // -----------------------------------------------------------------------

        internal static LocExtStringMod Fly_Grounded = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Can't takeoff, Too damaged / parts missing"},
            { LocalisationEnums.Languages.Japanese, "離陸できません。損傷がひどいか、部品が欠落しています"},
        });
        internal static LocExtStringMod Fly_Crashed = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Crashed"},
            { LocalisationEnums.Languages.Japanese, "クラッシュした"},
        });


        internal static LocExtStringMod Fly_Dive = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"Dive - "},
            { LocalisationEnums.Languages.Japanese, "急降下爆弾 - "},
        });
        internal static LocExtStringMod Fly_Dive2 = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"diving!"},
            { LocalisationEnums.Languages.Japanese, "ターゲットに向かってダイビング!"},
        });


        internal static LocExtStringMod Fly_UTurn = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"U-Turn - "},
            { LocalisationEnums.Languages.Japanese, "Uターン - "},
        });
        internal static LocExtStringMod Fly_UTurn2 = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English,"point upwards"},
            { LocalisationEnums.Languages.Japanese, "上を向いて"},
        });
    }
}
