using System;
using WebRPGShared;

namespace WebRPG
{
    // 集中單人模型操作與多人指令傳送，保留伺服器權威及暫停規則。
    public sealed class AdventureOperationHelper
    {
        private readonly AdventureModel _model;
        private readonly MultiplayerAdventureSession _network;

        public AdventureOperationHelper(AdventureModel model, MultiplayerAdventureSession network = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _network = network;
        }

        public bool IsMultiplayer => _network != null;

        public bool Start()
        {
            if (_network != null) return false;
            _model.StartRun();
            return true;
        }

        public bool Abandon()
        {
            if (_network != null)
            {
                _network.SendCommand(AdventureCommand.Abandon);
                return false;
            }
            _model.AbandonRun();
            return true;
        }

        public void Choose(int index)
        {
            if (_network != null)
            {
                switch (_model.Phase)
                {
                    case AdventurePhase.Map: _network.SendCommand(AdventureCommand.Vote, index); break;
                    case AdventurePhase.NodeChoice: _network.SendCommand(AdventureCommand.Resolve, index); break;
                    case AdventurePhase.SkillReward: _network.SendCommand(AdventureCommand.Reward, index); break;
                }
                return;
            }
            switch (_model.Phase)
            {
                case AdventurePhase.Map: _model.SelectNode(index); break;
                case AdventurePhase.SkillReward: _model.SelectReward(index); break;
                case AdventurePhase.NodeChoice: _model.ResolveNode(index); break;
            }
        }

        public void Target(int index)
        {
            if (_network == null) _model.LockEnemy(index);
            else _network.SendCommand(AdventureCommand.Target, index);
        }

        public void UseSkill(int index)
        {
            if (_network == null) _model.TryUseSkill(index);
            else _network.SendCommand(AdventureCommand.Skill, index);
        }

        public void SetBattleSpeed(float speed)
        {
            if (_network == null) _model.SetBattleSpeed(speed);
        }

        public void Tick(float seconds, bool paused)
        {
            if (_network != null) _network.Tick(seconds);
            else if (!paused && _model.Phase == AdventurePhase.Battle) _model.Tick(seconds);
        }
    }
}
