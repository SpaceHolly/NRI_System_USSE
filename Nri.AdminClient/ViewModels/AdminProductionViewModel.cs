using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminProductionViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _statusMessage = "Производство: мощности и factory orders готовы. Manufacturing stages/resources/cost/acceptance запускают фактическое изготовление только через действия GM.";
    private string _errorMessage = string.Empty;
    private ProductionUiItem? _selectedFacility;
    private ProductionUiItem? _selectedQuote;
    private ProductionUiItem? _selectedOrder;
    private ProductionUiItem? _selectedManufacturingProject;
    private ProductionUiItem? _selectedResourcePlan;
    private ProductionUiItem? _selectedReservation;
    private ProductionUiItem? _selectedPayment;
    private ProductionUiItem? _selectedDefect;
    private ProductionUiItem? _selectedAsset;

    public AdminProductionViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(RefreshAll);
        CreateFacilityDefinitionCommand = new RelayCommand(CreateFacilityDefinition);
        CreateFacilityCommand = new RelayCommand(CreateFacility);
        SaveFacilityCommand = new RelayCommand(SaveFacility);
        AddCapabilityCommand = new RelayCommand(AddCapability);
        SaveCapacityCommand = new RelayCommand(SaveCapacity);
        CreateProcessCommand = new RelayCommand(CreateProcess);
        GenerateQuoteCommand = new RelayCommand(GenerateQuote);
        OfferQuoteCommand = new RelayCommand(() => QuoteAction(_api.FactoryQuoteOffer, "Оценка предложена игроку."));
        AcceptQuoteCommand = new RelayCommand(() => QuoteAction(_api.FactoryQuoteAccept, "Оценка принята GM."));
        RejectQuoteCommand = new RelayCommand(() => QuoteAction(_api.FactoryQuoteReject, "Оценка отклонена."));
        ConvertQuoteToOrderCommand = new RelayCommand(ConvertQuoteToOrder);
        CreateOrderCommand = new RelayCommand(CreateOrder);
        ApproveOrderCommand = new RelayCommand(() => OrderAction(_api.FactoryOrderApprove, "Заказ утверждён."));
        ScheduleOrderCommand = new RelayCommand(() => OrderAction(_api.FactoryOrderSchedule, "Заказ поставлен в очередь."));
        CancelOrderCommand = new RelayCommand(() => OrderAction(_api.FactoryOrderCancel, "Заказ отменён."));
        ArchiveOrderCommand = new RelayCommand(() => OrderAction(_api.FactoryOrderArchive, "Заказ архивирован."));
        ReserveQueueCommand = new RelayCommand(() => OrderAction(_api.FactoryQueueReserve, "Слот очереди зарезервирован."));
        CreateManufacturingFromOrderCommand = new RelayCommand(CreateManufacturingFromOrder);
        CreateManualManufacturingCommand = new RelayCommand(CreateManualManufacturing);
        StartManufacturingCommand = new RelayCommand(() => ManufacturingAction(_api.ManufacturingProjectStart, "Производство запущено."));
        PauseManufacturingCommand = new RelayCommand(() => ManufacturingAction(_api.ManufacturingProjectPause, "Производство приостановлено."));
        ResumeManufacturingCommand = new RelayCommand(() => ManufacturingAction(_api.ManufacturingProjectResume, "Производство возобновлено."));
        CancelManufacturingCommand = new RelayCommand(() => ManufacturingAction(_api.ManufacturingProjectCancel, "Производство отменено; незатраченные резервы освобождены."));
        AddStageCommand = new RelayCommand(AddStage);
        StartStageCommand = new RelayCommand(() => StageAction(_api.ManufacturingStageStart, "Стадия запущена."));
        CompleteStageCommand = new RelayCommand(() => StageAction(_api.ManufacturingStageComplete, "Стадия завершена."));
        AddResourcePlanCommand = new RelayCommand(AddResourcePlan);
        ReserveResourceCommand = new RelayCommand(ReserveResource);
        ConsumeResourceCommand = new RelayCommand(ConsumeResource);
        AddCostCommand = new RelayCommand(AddCost);
        AddPaymentCommand = new RelayCommand(AddPayment);
        MarkPaymentPaidCommand = new RelayCommand(MarkPaymentPaid);
        AddProgressCommand = new RelayCommand(AddProgress);
        CreateTestPlanCommand = new RelayCommand(CreateTestPlan);
        AddPassedTestCommand = new RelayCommand(() => AddTestResult("passed"));
        AddFailedTestCommand = new RelayCommand(() => AddTestResult("failed"));
        CreateDefectCommand = new RelayCommand(CreateDefect);
        ResolveDefectCommand = new RelayCommand(ResolveDefect);
        PrepareAcceptanceCommand = new RelayCommand(PrepareAcceptance);
        AcceptManufacturingCommand = new RelayCommand(AcceptManufacturing);
        RejectAcceptanceCommand = new RelayCommand(() => ManufacturingAction(_api.ManufacturingAcceptanceReject, "Приёмка отклонена, проект отправлен на доработку."));
        CreateAssetCommand = new RelayCommand(CreateAsset);
        TransferAssetCommand = new RelayCommand(TransferAsset);
        CommissionAssetCommand = new RelayCommand(CommissionAsset);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<ProductionUiItem> FacilityDefinitions { get; } = new();
    public ObservableCollection<ProductionUiItem> Facilities { get; } = new();
    public ObservableCollection<ProductionUiItem> Capabilities { get; } = new();
    public ObservableCollection<ProductionUiItem> Processes { get; } = new();
    public ObservableCollection<ProductionUiItem> Quotes { get; } = new();
    public ObservableCollection<ProductionUiItem> Orders { get; } = new();
    public ObservableCollection<ProductionUiItem> QueueSlots { get; } = new();
    public ObservableCollection<ProductionUiItem> ManufacturingProjects { get; } = new();
    public ObservableCollection<ProductionUiItem> Stages { get; } = new();
    public ObservableCollection<ProductionUiItem> ResourcePlans { get; } = new();
    public ObservableCollection<ProductionUiItem> Reservations { get; } = new();
    public ObservableCollection<ProductionUiItem> Payments { get; } = new();
    public ObservableCollection<ProductionUiItem> Tests { get; } = new();
    public ObservableCollection<ProductionUiItem> Defects { get; } = new();
    public ObservableCollection<ProductionUiItem> ManufacturedAssets { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreateFacilityDefinitionCommand { get; }
    public ICommand CreateFacilityCommand { get; }
    public ICommand SaveFacilityCommand { get; }
    public ICommand AddCapabilityCommand { get; }
    public ICommand SaveCapacityCommand { get; }
    public ICommand CreateProcessCommand { get; }
    public ICommand GenerateQuoteCommand { get; }
    public ICommand OfferQuoteCommand { get; }
    public ICommand AcceptQuoteCommand { get; }
    public ICommand RejectQuoteCommand { get; }
    public ICommand ConvertQuoteToOrderCommand { get; }
    public ICommand CreateOrderCommand { get; }
    public ICommand ApproveOrderCommand { get; }
    public ICommand ScheduleOrderCommand { get; }
    public ICommand CancelOrderCommand { get; }
    public ICommand ArchiveOrderCommand { get; }
    public ICommand ReserveQueueCommand { get; }
    public ICommand CreateManufacturingFromOrderCommand { get; }
    public ICommand CreateManualManufacturingCommand { get; }
    public ICommand StartManufacturingCommand { get; }
    public ICommand PauseManufacturingCommand { get; }
    public ICommand ResumeManufacturingCommand { get; }
    public ICommand CancelManufacturingCommand { get; }
    public ICommand AddStageCommand { get; }
    public ICommand StartStageCommand { get; }
    public ICommand CompleteStageCommand { get; }
    public ICommand AddResourcePlanCommand { get; }
    public ICommand ReserveResourceCommand { get; }
    public ICommand ConsumeResourceCommand { get; }
    public ICommand AddCostCommand { get; }
    public ICommand AddPaymentCommand { get; }
    public ICommand MarkPaymentPaidCommand { get; }
    public ICommand AddProgressCommand { get; }
    public ICommand CreateTestPlanCommand { get; }
    public ICommand AddPassedTestCommand { get; }
    public ICommand AddFailedTestCommand { get; }
    public ICommand CreateDefectCommand { get; }
    public ICommand ResolveDefectCommand { get; }
    public ICommand PrepareAcceptanceCommand { get; }
    public ICommand AcceptManufacturingCommand { get; }
    public ICommand RejectAcceptanceCommand { get; }
    public ICommand CreateAssetCommand { get; }
    public ICommand TransferAssetCommand { get; }
    public ICommand CommissionAssetCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get; set; } = "default";
    public string RuleSetId { get; set; } = "default";
    public string NewFacilityName { get; set; } = "Новая мастерская";
    public string NewFacilityCategory { get; set; } = "engineering_workshop";
    public string NewFacilityType { get; set; } = "guild";
    public string NewFacilityDomain { get; set; } = "vehicle_manufacturing";
    public string NewFacilitySizeClass { get; set; } = "medium";
    public string NewFacilityPlatformCategory { get; set; } = "ground_vehicle";
    public string NewFacilityModuleCategory { get; set; } = "engine";
    public string CapacityRating { get; set; } = "2";
    public string MaxQueueSlots { get; set; } = "2";
    public string CurrentLoadPercent { get; set; } = "0";
    public string QuoteName { get; set; } = "Оценка производства";
    public string QuoteBlueprintId { get; set; } = string.Empty;
    public string QuotePresetId { get; set; } = string.Empty;
    public string QuoteOwnerUserId { get; set; } = string.Empty;
    public string QuoteOwnerCharacterId { get; set; } = string.Empty;
    public string QuoteFacilityId { get; set; } = string.Empty;
    public string StageName { get; set; } = "Стадия производства";
    public string StageType { get; set; } = "fabrication";
    public string ResourceName { get; set; } = "Материалы";
    public string ResourceQuantity { get; set; } = "1";
    public string ResourceUnit { get; set; } = "pcs";
    public string CostAmount { get; set; } = "100";
    public string PaymentAmount { get; set; } = "100";
    public string ProgressDelta { get; set; } = "10";
    public string TestPlanName { get; set; } = "Испытания результата";
    public string DefectSummary { get; set; } = "Замечание по результату";
    public bool DefectCritical { get; set; }
    public bool VisibleToPlayers { get; set; } = true;
    public bool GmOverride { get; set; }

    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }

    public ProductionUiItem? SelectedFacility { get => _selectedFacility; set { if (_selectedFacility != value) { _selectedFacility = value; if (value != null) QuoteFacilityId = value.Id; Notify(); Notify(nameof(QuoteFacilityId)); } } }
    public ProductionUiItem? SelectedQuote { get => _selectedQuote; set { if (_selectedQuote != value) { _selectedQuote = value; Notify(); } } }
    public ProductionUiItem? SelectedOrder { get => _selectedOrder; set { if (_selectedOrder != value) { _selectedOrder = value; Notify(); } } }
    public ProductionUiItem? SelectedManufacturingProject
    {
        get => _selectedManufacturingProject;
        set
        {
            if (_selectedManufacturingProject != value)
            {
                _selectedManufacturingProject = value;
                Notify();
                LoadManufacturingDetails();
            }
        }
    }
    public ProductionUiItem? SelectedResourcePlan { get => _selectedResourcePlan; set { if (_selectedResourcePlan != value) { _selectedResourcePlan = value; Notify(); } } }
    public ProductionUiItem? SelectedReservation { get => _selectedReservation; set { if (_selectedReservation != value) { _selectedReservation = value; Notify(); } } }
    public ProductionUiItem? SelectedPayment { get => _selectedPayment; set { if (_selectedPayment != value) { _selectedPayment = value; Notify(); } } }
    public ProductionUiItem? SelectedDefect { get => _selectedDefect; set { if (_selectedDefect != value) { _selectedDefect = value; Notify(); } } }
    public ProductionUiItem? SelectedAsset { get => _selectedAsset; set { if (_selectedAsset != value) { _selectedAsset = value; Notify(); } } }

    public void RefreshAll()
    {
        try
        {
            ErrorMessage = string.Empty;
            LoadList(_api.ProductionFacilityDefinitionList(BasePayload()), FacilityDefinitions, "типы мощностей");
            LoadList(_api.ProductionFacilityList(BasePayload()), Facilities, "мощности");
            LoadList(_api.ProductionProcessList(BasePayload()), Processes, "процессы");
            LoadList(_api.FactoryQuoteList(BasePayload()), Quotes, "оценки");
            LoadList(_api.FactoryOrderList(BasePayload()), Orders, "заказы");
            LoadList(_api.FactoryQueueList(BasePayload()), QueueSlots, "очередь");
            LoadList(_api.ManufacturingProjectList(BasePayload()), ManufacturingProjects, "производственные проекты");
            LoadList(_api.ManufacturingAssetList(BasePayload()), ManufacturedAssets, "произведённые активы");
            LoadManufacturingDetails();
            StatusMessage = "Производственные данные обновлены.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Раздел производства недоступен или выключен флагами функций.";
            ClientLogService.Instance.Error("admin.production.refresh.error", ex);
        }
    }

    private void LoadManufacturingDetails()
    {
        Stages.Clear();
        ResourcePlans.Clear();
        Reservations.Clear();
        Payments.Clear();
        Tests.Clear();
        Defects.Clear();
        if (SelectedManufacturingProject == null) return;
        var response = _api.ManufacturingProjectGet(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id });
        if (response.Status != ResponseStatus.Ok) return;
        if (!response.Payload.TryGetValue("item", out var raw)) return;
        var item = ToDictionary(raw);
        AddNestedItems(item, "stages", Stages);
        AddNestedItems(item, "resourcePlans", ResourcePlans);
        AddNestedItems(item, "reservations", Reservations);
        AddNestedItems(item, "payments", Payments);
        AddNestedItems(item, "testResults", Tests);
        AddNestedItems(item, "defects", Defects);
        AddNestedItems(item, "assets", ManufacturedAssets);
    }

    private void CreateFacilityDefinition() => Execute(() => _api.ProductionFacilityDefinitionCreate(new Dictionary<string, object>(BasePayload())
    {
        ["name"] = NewFacilityName, ["facilityCategory"] = NewFacilityCategory, ["facilityType"] = NewFacilityType,
        ["supportedProductionDomains"] = NewFacilityDomain, ["supportedPlatformCategories"] = NewFacilityPlatformCategory,
        ["supportedSizeClassIds"] = NewFacilitySizeClass, ["supportedModuleCategories"] = NewFacilityModuleCategory,
        ["baseCapacityRating"] = CapacityRating, ["isPlayerVisible"] = VisibleToPlayers,
        ["visibilityMode"] = VisibleToPlayers ? "player_visible" : "gm_only"
    }), "Тип производственной мощности создан.");

    private void CreateFacility() => Execute(() => _api.ProductionFacilityCreate(new Dictionary<string, object>(BasePayload())
    {
        ["name"] = NewFacilityName, ["facilityCategory"] = NewFacilityCategory, ["facilityType"] = NewFacilityType,
        ["supportedProductionDomains"] = NewFacilityDomain, ["supportedPlatformCategories"] = NewFacilityPlatformCategory,
        ["supportedSizeClassIds"] = NewFacilitySizeClass, ["supportedModuleCategories"] = NewFacilityModuleCategory,
        ["capacityRating"] = CapacityRating, ["operationalStatus"] = "active", ["isPlayerVisible"] = VisibleToPlayers,
        ["visibilityMode"] = VisibleToPlayers ? "player_visible" : "gm_only"
    }), "Производственная мощность создана.");

    private void SaveFacility()
    {
        if (SelectedFacility == null) { ErrorMessage = "Выберите производственную мощность."; return; }
        Execute(() => _api.ProductionFacilityUpdate(new Dictionary<string, object>(BasePayload())
        {
            ["id"] = SelectedFacility.Id, ["name"] = NewFacilityName, ["facilityCategory"] = NewFacilityCategory,
            ["facilityType"] = NewFacilityType, ["supportedProductionDomains"] = NewFacilityDomain,
            ["capacityRating"] = CapacityRating, ["isPlayerVisible"] = VisibleToPlayers,
            ["visibilityMode"] = VisibleToPlayers ? "player_visible" : "gm_only"
        }), "Производственная мощность обновлена.");
    }

    private void AddCapability()
    {
        if (SelectedFacility == null) { ErrorMessage = "Выберите производственную мощность."; return; }
        Execute(() => _api.ProductionCapabilityAdd(new Dictionary<string, object>(BasePayload())
        {
            ["facilityId"] = SelectedFacility.Id, ["productionDomain"] = NewFacilityDomain,
            ["supportedPlatformCategories"] = NewFacilityPlatformCategory, ["supportedSizeClassIds"] = NewFacilitySizeClass,
            ["supportedModuleCategories"] = NewFacilityModuleCategory, ["capacityRating"] = CapacityRating,
            ["isPlayerVisible"] = VisibleToPlayers, ["publicSummary"] = "Поддерживаемая производственная возможность."
        }), "Возможность добавлена.");
    }

    private void SaveCapacity()
    {
        if (SelectedFacility == null) { ErrorMessage = "Выберите производственную мощность."; return; }
        Execute(() => _api.ProductionCapacityUpdate(new Dictionary<string, object>(BasePayload())
        {
            ["facilityId"] = SelectedFacility.Id, ["capacityRating"] = CapacityRating,
            ["maxQueueSlots"] = MaxQueueSlots, ["currentLoadPercent"] = CurrentLoadPercent
        }), "Емкость и очередь обновлены.");
    }

    private void CreateProcess() => Execute(() => _api.ProductionProcessCreate(new Dictionary<string, object>(BasePayload())
    {
        ["name"] = "Базовый производственный процесс", ["productionDomain"] = NewFacilityDomain,
        ["baseWorkPoints"] = "100", ["isPlayerVisible"] = VisibleToPlayers
    }), "Производственный процесс создан.");

    private void GenerateQuote() => Execute(() => _api.FactoryQuoteGenerate(QuotePayload()), "Factory quote создан. Производство ещё не запущено.");
    private void CreateOrder() => Execute(() => _api.FactoryOrderCreate(QuotePayload()), "Factory order создан как ожидание производства.");

    private void ConvertQuoteToOrder()
    {
        if (SelectedQuote == null) { ErrorMessage = "Выберите оценку."; return; }
        Execute(() => _api.FactoryQuoteConvertToOrder(new Dictionary<string, object>(BasePayload()) { ["quoteId"] = SelectedQuote.Id, ["gmOverride"] = true }), "Оценка превращена в заказ.");
    }

    private void CreateManufacturingFromOrder()
    {
        if (SelectedOrder == null) { ErrorMessage = "Выберите factory order."; return; }
        Execute(() => _api.ManufacturingProjectCreateFromOrder(new Dictionary<string, object>(BasePayload()) { ["orderId"] = SelectedOrder.Id }), "Производственный проект создан из заказа.");
    }

    private void CreateManualManufacturing() => Execute(() => _api.ManufacturingProjectCreateManual(new Dictionary<string, object>(BasePayload())
    {
        ["name"] = QuoteName, ["facilityId"] = FirstNonEmpty(QuoteFacilityId, SelectedFacility?.Id), ["blueprintId"] = QuoteBlueprintId,
        ["presetId"] = QuotePresetId, ["productionDomain"] = NewFacilityDomain, ["manufacturingType"] = "vehicle_build",
        ["estimatedTotalCost"] = CostAmount, ["expectedResultSummary"] = QuoteName, ["isPlayerVisible"] = VisibleToPlayers,
        ["visibilityMode"] = VisibleToPlayers ? "owner_only" : "gm_only", ["ownerEntityType"] = string.IsNullOrWhiteSpace(QuoteOwnerCharacterId) ? "user" : "character",
        ["ownerEntityId"] = FirstNonEmpty(QuoteOwnerCharacterId, QuoteOwnerUserId)
    }), "Производственный проект создан вручную.");

    private void AddStage()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingStageAdd(new Dictionary<string, object>(BasePayload())
        {
            ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["name"] = StageName, ["stageType"] = StageType,
            ["requiredProgress"] = ProgressDelta, ["isPlayerVisible"] = VisibleToPlayers
        }), "Стадия добавлена.");
    }

    private void AddResourcePlan()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingResourcePlanAdd(new Dictionary<string, object>(BasePayload())
        {
            ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["resourceName"] = ResourceName,
            ["requiredQuantity"] = ResourceQuantity, ["unit"] = ResourceUnit, ["isPlayerVisible"] = VisibleToPlayers
        }), "План ресурсов добавлен.");
    }

    private void ReserveResource()
    {
        if (SelectedResourcePlan == null) { ErrorMessage = "Выберите ресурсный план."; return; }
        Execute(() => _api.ManufacturingResourceReserve(new Dictionary<string, object>(BasePayload()) { ["resourcePlanId"] = SelectedResourcePlan.Id, ["quantity"] = ResourceQuantity }), "Ресурс зарезервирован вручную.");
    }

    private void ConsumeResource()
    {
        if (SelectedReservation == null) { ErrorMessage = "Выберите конкретную резервацию ресурса."; return; }
        Execute(() => _api.ManufacturingResourceConsume(new Dictionary<string, object>(BasePayload()) { ["reservationId"] = SelectedReservation.Id, ["quantity"] = ResourceQuantity }), "Ресурс списан вручную.");
    }

    private void AddCost()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingCostAdd(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["amount"] = CostAmount, ["costType"] = "manual", ["isEstimated"] = false, ["isPlayerVisible"] = VisibleToPlayers }), "Затрата добавлена.");
    }

    private void AddPayment()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingPaymentAdd(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["amount"] = PaymentAmount, ["paymentKind"] = "deposit", ["isPlayerVisible"] = VisibleToPlayers }), "Платёж добавлен.");
    }

    private void MarkPaymentPaid()
    {
        if (SelectedPayment == null) { ErrorMessage = "Выберите платёж."; return; }
        Execute(() => _api.ManufacturingPaymentMarkPaid(new Dictionary<string, object>(BasePayload()) { ["paymentId"] = SelectedPayment.Id }), "Платёж отмечен как оплаченный вручную.");
    }

    private void AddProgress()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingProgressAdd(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["progressDelta"] = ProgressDelta, ["isPlayerVisible"] = VisibleToPlayers }), "Прогресс добавлен.");
    }

    private void CreateTestPlan()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingTestPlanCreate(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["name"] = TestPlanName, ["isPlayerVisible"] = VisibleToPlayers }), "План испытаний создан.");
    }

    private void AddTestResult(string result)
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingTestResultAdd(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["result"] = result, ["publicSummary"] = result == "passed" ? "Испытание пройдено." : "Испытание выявило проблему.", ["isPlayerVisible"] = VisibleToPlayers }), "Результат испытаний добавлен.");
    }

    private void CreateDefect()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingDefectCreate(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["publicSummary"] = DefectSummary, ["isCritical"] = DefectCritical, ["isPlayerVisible"] = VisibleToPlayers }), "Дефект/замечание создан.");
    }

    private void ResolveDefect()
    {
        if (SelectedDefect == null) { ErrorMessage = "Выберите дефект."; return; }
        Execute(() => _api.ManufacturingDefectResolve(new Dictionary<string, object>(BasePayload()) { ["defectId"] = SelectedDefect.Id }), "Дефект закрыт.");
    }

    private void PrepareAcceptance()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingAcceptancePrepare(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["isPlayerVisible"] = VisibleToPlayers }), "Результат подготовлен к приёмке.");
    }

    private void AcceptManufacturing()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingAcceptanceAccept(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["gmOverride"] = GmOverride, ["isPlayerVisible"] = VisibleToPlayers }), "Результат принят GM. Теперь можно создать asset.");
    }

    private void CreateAsset()
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => _api.ManufacturingAssetCreate(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id, ["name"] = QuoteName, ["assetType"] = "vehicle_asset" }), "Произведённый asset создан после приёмки.");
    }

    private void TransferAsset()
    {
        if (SelectedAsset == null) { ErrorMessage = "Выберите произведённый asset."; return; }
        Execute(() => _api.ManufacturingAssetTransfer(new Dictionary<string, object>(BasePayload()) { ["assetId"] = SelectedAsset.Id, ["ownerEntityType"] = string.IsNullOrWhiteSpace(QuoteOwnerCharacterId) ? "user" : "character", ["ownerEntityId"] = FirstNonEmpty(QuoteOwnerCharacterId, QuoteOwnerUserId) }), "Asset передан владельцу.");
    }

    private void CommissionAsset()
    {
        if (SelectedAsset == null) { ErrorMessage = "Выберите произведённый asset."; return; }
        Execute(() => _api.ManufacturingAssetCommission(new Dictionary<string, object>(BasePayload()) { ["assetId"] = SelectedAsset.Id }), "Asset введён в эксплуатацию.");
    }

    private void QuoteAction(Func<Dictionary<string, object>, ResponseEnvelope> action, string message)
    {
        if (SelectedQuote == null) { ErrorMessage = "Выберите оценку."; return; }
        Execute(() => action(new Dictionary<string, object>(BasePayload()) { ["quoteId"] = SelectedQuote.Id }), message);
    }

    private void OrderAction(Func<Dictionary<string, object>, ResponseEnvelope> action, string message)
    {
        if (SelectedOrder == null) { ErrorMessage = "Выберите заказ."; return; }
        Execute(() => action(new Dictionary<string, object>(BasePayload()) { ["orderId"] = SelectedOrder.Id }), message);
    }

    private void ManufacturingAction(Func<Dictionary<string, object>, ResponseEnvelope> action, string message)
    {
        if (SelectedManufacturingProject == null) { ErrorMessage = "Выберите производственный проект."; return; }
        Execute(() => action(new Dictionary<string, object>(BasePayload()) { ["manufacturingProjectId"] = SelectedManufacturingProject.Id }), message);
    }

    private void StageAction(Func<Dictionary<string, object>, ResponseEnvelope> action, string message)
    {
        var stage = Stages.FirstOrDefault();
        if (stage == null) { ErrorMessage = "Нет стадии для действия."; return; }
        Execute(() => action(new Dictionary<string, object>(BasePayload()) { ["stageId"] = stage.Id }), message);
    }

    private void Execute(Func<ResponseEnvelope> action, string success)
    {
        try
        {
            var response = action();
            if (response.Status != ResponseStatus.Ok)
            {
                ErrorMessage = response.Message;
                StatusMessage = "Операция не выполнена.";
                return;
            }

            StatusMessage = success;
            AuditRows.Insert(0, $"{DateTime.Now:HH:mm:ss} {success}");
            RefreshAll();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Error("admin.production.error", ex);
        }
    }

    private void LoadList(ResponseEnvelope response, ObservableCollection<ProductionUiItem> target, string label)
    {
        target.Clear();
        if (response.Status != ResponseStatus.Ok)
        {
            ErrorMessage = $"{label}: {response.Message}";
            return;
        }

        foreach (var item in ExtractItems(response))
            target.Add(ProductionUiItem.From(item));
    }

    private Dictionary<string, object> QuotePayload() => new(BasePayload())
    {
        ["name"] = QuoteName,
        ["facilityId"] = FirstNonEmpty(QuoteFacilityId, SelectedFacility?.Id),
        ["blueprintId"] = QuoteBlueprintId,
        ["presetId"] = QuotePresetId,
        ["ownerUserId"] = QuoteOwnerUserId,
        ["ownerCharacterId"] = QuoteOwnerCharacterId,
        ["sourceType"] = string.IsNullOrWhiteSpace(QuoteBlueprintId) ? (string.IsNullOrWhiteSpace(QuotePresetId) ? "custom" : "preset") : "blueprint",
        ["isPlayerVisible"] = VisibleToPlayers,
        ["visibilityMode"] = VisibleToPlayers ? "owner_only" : "gm_only"
    };

    private Dictionary<string, object> BasePayload() => new() { ["campaignId"] = CampaignId, ["ruleSetId"] = RuleSetId };

    private static void AddNestedItems(IDictionary<string, object> parent, string key, ObservableCollection<ProductionUiItem> target)
    {
        target.Clear();
        if (!parent.TryGetValue(key, out var raw) || raw == null) return;
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable)
            {
                var map = ToDictionary(item);
                if (map.Count > 0) target.Add(ProductionUiItem.From(map));
            }
        }
    }

    private static IEnumerable<IDictionary<string, object>> ExtractItems(ResponseEnvelope response)
    {
        if (!response.Payload.TryGetValue("items", out var raw) || raw == null) yield break;
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable)
            {
                var map = ToDictionary(item);
                if (map.Count > 0) yield return map;
            }
        }
    }

    private static Dictionary<string, object> ToDictionary(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    private static string FirstNonEmpty(params string?[] values) => values.Select(x => (x ?? string.Empty).Trim()).FirstOrDefault(x => x.Length > 0) ?? string.Empty;
}

public sealed class ProductionUiItem
{
    public string Id { get; set; } = string.Empty;
    public string SecondaryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Secondary { get; set; } = string.Empty;

    public static ProductionUiItem From(IDictionary<string, object> map)
    {
        var id = Get(map, "id");
        var name = First(Get(map, "name"), Get(map, "displayName"), Get(map, "resourceName"), id, "Без названия");
        var status = First(Get(map, "status"), Get(map, "manufacturingStatus"), Get(map, "operationalStatus"), Get(map, "facilityValidationStatus"), "—");
        var type = First(Get(map, "facilityCategory"), Get(map, "sourceType"), Get(map, "productionDomain"), Get(map, "manufacturingType"), Get(map, "stageType"), Get(map, "assetType"), "—");
        var cost = First(Get(map, "estimatedCost"), Get(map, "estimatedTotalCost"), Get(map, "amount"));
        var progress = Get(map, "progressPercent");
        var summary = !string.IsNullOrWhiteSpace(progress)
            ? $"{name} · {status} · {progress}%"
            : string.IsNullOrWhiteSpace(cost) ? $"{name} · {type} · {status}" : $"{name} · {status} · {cost} МО";
        return new ProductionUiItem
        {
            Id = id,
            SecondaryId = First(Get(map, "reservationId"), Get(map, "assetStateId")),
            Name = name,
            Status = status,
            Type = type,
            Summary = summary,
            Secondary = First(Get(map, "riskSummary"), Get(map, "publicStatusSummary"), Get(map, "resourceRequirementSummary"), Get(map, "paymentPlanSummary"), Get(map, "publicSummary"), Get(map, "description"), Get(map, "publicTermsSummary"), "—")
        };
    }

    private static string Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
