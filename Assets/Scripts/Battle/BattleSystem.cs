using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum BattleState { Start, ActionSelection, MoveSelection, PerformMove, Busy, PartyScreen, BattleOver}
public class BattleSystem : MonoBehaviour
{
    [SerializeField] BattleUnit playerUnit;
    [SerializeField] BattleUnit enemyUnit;

    
    [SerializeField] BattleDialogBox dialogBox;
    [SerializeField] PartyScreen partyScreen;

    public event Action<bool> OnBattleOver;

    BattleState state;
    int currentAction;
    int currentMove;
    int currentMember;

    PokemonParty playerParty;
    Pokemon wildPokemon;


    public void StartBattle(PokemonParty playerParty, Pokemon wildPokemon)
    {
        this.playerParty = playerParty;
        this.wildPokemon = wildPokemon;
        StartCoroutine(SetupBattle());
    }

    private IEnumerator SetupBattle()
    {
        playerUnit.Setup(playerParty.GetHealthyPokemon());
        enemyUnit.Setup(wildPokemon);
        

        partyScreen.Init();

        dialogBox.SetMoveNames(playerUnit.Pokemon.Moves);

        yield return dialogBox.TypeDialog($"A wild {enemyUnit.Pokemon.Base.Name} appeared.");


        ChooseFirstTurn();
    }

    private void ChooseFirstTurn()
    {
        if (playerUnit.Pokemon.Speed >= enemyUnit.Pokemon.Speed )
            ActionSelection();
        else
            StartCoroutine(EnemyMove());
    }

    private void ActionSelection()
    {
        state = BattleState.ActionSelection;
        dialogBox.SetDialog("Choose an action");
        dialogBox.EnableActionSelector(true);
    }
    private void MoveSelection()
    {
        state = BattleState.MoveSelection;
        dialogBox.EnableActionSelector(false);
        dialogBox.EnableDialogText(false);
        dialogBox.EnableMoveSelector(true);
    }
    private void OpenPartyScreen()
    {
        state = BattleState.PartyScreen;
        partyScreen.SetPartyData(playerParty.Pokemons);
        partyScreen.gameObject.SetActive(true);
    }

    private void BattleOver(bool won)
    {
        state = BattleState.BattleOver;
        playerParty.Pokemons.ForEach(p => p.OnBattleOver());
        OnBattleOver(won);
    }


    public void HandleUpdate()
    {
        if (state == BattleState.ActionSelection)
        {
            HandleActionSelection();
        }
        else if (state == BattleState.MoveSelection)
        {
            HandleMoveSelection();
        }
        else if (state == BattleState.PartyScreen)
        {
            HandlePartySelection();
        }
    }

    private void HandlePartySelection()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow)) { ++currentMember; }
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) { --currentMember; }
        else if (Input.GetKeyDown(KeyCode.DownArrow)) { currentMember += 2; }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { currentMember -= 2; }

        currentMember = Mathf.Clamp(currentMember, 0, playerParty.Pokemons.Count - 1);

        partyScreen.UpdateMemberSelection(currentMember);

        if (Input.GetKeyDown(KeyCode.Z))
        {
            var selectedMember = playerParty.Pokemons[currentMember];
            if (selectedMember.HP <= 0) 
            {
                partyScreen.SetMessageText("You can't send out a fainted pokemon.");
                return;
            }
            if (selectedMember == playerUnit.Pokemon)
            {
                partyScreen.SetMessageText("You can't switch with the same pokemon.");
                return;
            }
            partyScreen.gameObject.SetActive(false);

            state = BattleState.Busy;
            StartCoroutine(SwitchPokemon(selectedMember));

        }
        else if (Input.GetKeyDown(KeyCode.X))
        {
            partyScreen.gameObject.SetActive(false);
            ActionSelection();
        }
    }

    private IEnumerator SwitchPokemon(Pokemon newPokemon)
    {
        bool currentPokemonFainted = true;

        if (playerUnit.Pokemon.HP > 0)
        {
            currentPokemonFainted = false;
            yield return dialogBox.TypeDialog($"Come back {playerUnit.Pokemon.Base.Name}");
            playerUnit.PlayFaintAnimation();
            yield return new WaitForSeconds(2f);
        }

        playerUnit.Setup(newPokemon);
    

        dialogBox.SetMoveNames(newPokemon.Moves);

        yield return dialogBox.TypeDialog($"Go {newPokemon.Base.Name}!");

        if (currentPokemonFainted)
            ChooseFirstTurn();
        else
        StartCoroutine(EnemyMove());
    }

    private void HandleMoveSelection()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow)) { ++currentMove; }
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) { --currentMove; }
        else if (Input.GetKeyDown(KeyCode.DownArrow)) { currentMove += 2; }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { currentMove -= 2; }

        currentMove = Mathf.Clamp(currentMove, 0, playerUnit.Pokemon.Moves.Count - 1);

        dialogBox.UpdateMoveSelection(currentMove, playerUnit.Pokemon.Moves[currentMove]);

        if (Input.GetKeyDown(KeyCode.Z))
        {
            dialogBox.EnableMoveSelector(false);
            dialogBox.EnableDialogText(true);
            StartCoroutine(PlayerMove());
        }
        else if (Input.GetKeyDown(KeyCode.X))
        {
            dialogBox.EnableMoveSelector(false);
            dialogBox.EnableDialogText(true);
            ActionSelection();
        }
    }

    private IEnumerator PlayerMove()
    {
        state = BattleState.PerformMove;

        var move = playerUnit.Pokemon.Moves[currentMove];
        yield return RunMove(playerUnit, enemyUnit, move);
       
        // if state not changed by RunMove, go to next step
        if (state == BattleState.PerformMove)
            StartCoroutine(EnemyMove());
        
    }

    private IEnumerator EnemyMove()
    {
        state = BattleState.PerformMove;

        var move = enemyUnit.Pokemon.GetRandomMove();
        yield return RunMove(enemyUnit, playerUnit, move);

        // if state not changed by RunMove, go to next step
        if (state == BattleState.PerformMove)
            ActionSelection();      
    }

    private IEnumerator RunMove(BattleUnit sourceUnit, BattleUnit targetUnit, Move move)
    {
        move.PP--;
        yield return dialogBox.TypeDialog($"{sourceUnit.Pokemon.Base.Name} used {move.Base.Name}");

        sourceUnit.PlayAttackAnimation();
        yield return new WaitForSeconds(1);
        targetUnit.PlayHitAnimation();

        if (move.Base.Category == MoveCategory.Status)
        {
            yield return RunMoveEffects(move, sourceUnit.Pokemon, targetUnit.Pokemon);
        }
        else
        {
            var damageDetails = targetUnit.Pokemon.TakeDamage(move, sourceUnit.Pokemon);
            yield return targetUnit.Hud.UpdateHP();
            yield return ShowDamageDetails(damageDetails);
        }
        if (targetUnit.Pokemon.HP <= 0)
        {
            yield return dialogBox.TypeDialog($"{targetUnit.Pokemon.Base.Name} Fainted");
            targetUnit.PlayFaintAnimation();

            yield return new WaitForSeconds(2f);

            CheckForBattleOver(targetUnit);
        }
    }

    private IEnumerator RunMoveEffects(Move move, Pokemon source, Pokemon target)
    {
        var effects = move.Base.Effects;

        // stat boosting
        if (effects.Boosts != null)
        {
            if (move.Base.Target == MoveTarget.Self)
                source.ApplyBoosts(effects.Boosts);
            else
                target.ApplyBoosts(effects.Boosts);
        }


        // status condition
        if (effects.Status != ConditionID.none)
        {
            target.SetStatus(effects.Status);
        }

        yield return ShowStatusChanges(source);
        yield return ShowStatusChanges(target);
    }

    void CheckForBattleOver(BattleUnit faintedUnit)
    {
        if (faintedUnit.IsPlayerUnit)
        {
            var nextPokemon = playerParty.GetHealthyPokemon();
            if (nextPokemon != null)
                OpenPartyScreen();
            else
                BattleOver(false); 
        }
        else       
            BattleOver(true);        
    }


    IEnumerator ShowStatusChanges(Pokemon pokemon)
    {
        while (pokemon.StatusChanges.Count > 0)
        {
            var message = pokemon.StatusChanges.Dequeue();
            yield return dialogBox.TypeDialog(message);
        }
    }

    IEnumerator ShowDamageDetails(DamageDetails damageDetails)
    {
        if (damageDetails.Critical >1f)
            yield return dialogBox.TypeDialog("A critical hit!");       
        if (damageDetails.TypeEffectiveness > 1f)       
            yield return dialogBox.TypeDialog("It's super effective!");
        else if (damageDetails.TypeEffectiveness < 1f)
            yield return dialogBox.TypeDialog("It's not very effective!");
    }

    private void HandleActionSelection()
    {
       
        if (Input.GetKeyDown(KeyCode.RightArrow)) { ++currentAction; }
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) { --currentAction; }
        else if (Input.GetKeyDown(KeyCode.DownArrow)) { currentAction += 2; }
        else if (Input.GetKeyDown(KeyCode.UpArrow)) { currentAction -= 2; }

        currentAction = Mathf.Clamp(currentAction, 0, 3);


        dialogBox.UpdateActionSelection(currentAction);

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (currentAction == 0)
            {
                //todo fight
                MoveSelection();
            }
            else if (currentAction == 1)
            {
                // todo bag
            }
            else if (currentAction == 2)
            {
                // todo pokemon
                OpenPartyScreen();
            }
            else if (currentAction == 3)
            {
                // todo run
            }
        }
    }

    
}
