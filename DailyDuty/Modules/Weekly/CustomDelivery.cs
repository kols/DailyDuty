﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DailyDuty.Data.Enums;
using DailyDuty.Data.SettingsObjects;
using DailyDuty.Data.SettingsObjects.WeeklySettings;
using DailyDuty.Interfaces;
using DailyDuty.Utilities;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Condition = DailyDuty.Utilities.Condition;

namespace DailyDuty.Modules.Weekly;

internal unsafe class CustomDelivery : 
    IConfigurable, 
    IUpdateable,
    IZoneChangeThrottledNotification,
    ICompletable,
    IWeeklyResettable
{
    private CustomDeliverySettings Settings => Service.Configuration.Current().CustomDelivery;
    public CompletionType Type => CompletionType.Weekly;
    public string HeaderText => "Custom Delivery";
    public GenericSettings GenericSettings => Settings;

    private bool exchangeStarted = false;
    private uint lastDeliveryCount = 0;

    public DateTime NextReset
    {
        get => Settings.NextReset;
        set => Settings.NextReset = value;
    }

    public bool IsCompleted()
    {
        return Settings.AllowancesRemaining == 0;
    }

    public void SendNotification()
    {
        if (Condition.IsBoundByDuty() == true) return;

        if (Settings.Enabled)
        {
            PrintRemainingAllowances();
        }
    }

    void IResettable.ResetThis(CharacterSettings settings)
    {
        var customDeliveriesSettings = settings.CustomDelivery;

        customDeliveriesSettings.AllowancesRemaining = 12;
    }

    public void Update()
    {
        if (Settings.Enabled)
        {
            // If we are occupied by talking to a quest npc
            if (Service.Condition[ConditionFlag.OccupiedInQuestEvent] == true)
            {
                // If a custom delivery window is open
                if (GetCustomDeliveryPointer() != null)
                {
                    StartCustomDeliveryExchange();
                }
                // If we started an exchange, check for cutscene event
                if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] == true && exchangeStarted == true)
                {
                    Settings.AllowancesRemaining -= 1;
                    Service.Configuration.Save();
                        
                    exchangeStarted = false;
                }
            }
            // End the exchange when we are no longer locked by OccupiedInQuestEvent
            else if(exchangeStarted == true)
            {
                exchangeStarted = false;
            }
        }
    }

    public void NotificationOptions()
    {        
        Draw.OnLoginReminderCheckbox(Settings, HeaderText);

        Draw.OnTerritoryChangeCheckbox(Settings, HeaderText);
    }

    public void EditModeOptions()
    {
        Draw.EditNumberField("Allowances", HeaderText, ref Settings.AllowancesRemaining);
    }

    public void DisplayData()
    {
        Draw.NumericDisplay("Allowances Remaining", Settings.AllowancesRemaining);
    }
    public void Dispose()
    {

    }

    //
    //  Implementation
    //
    private uint? GetRemainingDeliveriesCount()
    {
        var pointer = GetCustomDeliveryPointer();
        if (pointer == null) return null;

        var textNode = (AtkTextNode*) ((AtkUnitBase*) pointer)->GetNodeById(34);
        if (textNode == null) return null;

        var nodeText = textNode->NodeText.ToString();
        if(nodeText == string.Empty) return null;

        var resultString = Regex.Match(textNode->NodeText.ToString(), @"\d+").Value;

        uint number = uint.Parse(resultString);

        return number;
    }

    private AtkResNode* GetCustomDeliveryPointer()
    {
        return (AtkResNode*)Service.GameGui.GetAddonByName("SatisfactionSupply", 1);
    }

    private void StartCustomDeliveryExchange()
    {
        var count = GetRemainingDeliveriesCount();
        if (count == null) return;

        if (exchangeStarted == false)
        {
            exchangeStarted = true;
            lastDeliveryCount = count.Value;
        }
        else if (exchangeStarted == true)
        {
            if (count.Value != lastDeliveryCount)
            {
                lastDeliveryCount = count.Value;

                Settings.AllowancesRemaining -= 1;
                Service.Configuration.Save();
            }
        }
    }

    private void PrintRemainingAllowances()
    {
        if (Settings.AllowancesRemaining > 0)
        {
            Chat.Print(HeaderText, $"{Settings.AllowancesRemaining} Allowances Remaining");
        }
    }
}