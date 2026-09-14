unit UserScript;

uses
  Classes, SysUtils;

var
  inspectedFile: IInterface;
  sawPluginSelection: Boolean;
  invalidSelection: Boolean;

  totalRecords: Integer;

  cellTotal: Integer;
  cellPresent: Integer;
  cellAdded: Integer;
  cellChangedExisting: Integer;
  cellRemoved: Integer;
  cellUnchanged: Integer;
  cellPreviousUnresolved: Integer;
  cellPatchLinkUnresolved: Integer;
  cellPreviousLinkUnresolved: Integer;
  cellDeleted: Integer;

  wrldTotal: Integer;
  wrldPresent: Integer;
  wrldAdded: Integer;
  wrldChangedExisting: Integer;
  wrldRemoved: Integer;
  wrldUnchanged: Integer;
  wrldPreviousUnresolved: Integer;
  wrldPatchLinkUnresolved: Integer;
  wrldPreviousLinkUnresolved: Integer;
  wrldDeleted: Integer;

  ecznTotal: Integer;
  ecznBoundaryPresent: Integer;
  ecznBoundaryAdded: Integer;
  ecznBoundaryAlreadyPresent: Integer;
  ecznBoundaryRemoved: Integer;
  ecznBoundaryUnchangedAbsent: Integer;
  ecznPreviousUnresolved: Integer;
  ecznDeleted: Integer;


procedure ObserveFile(aFile: IInterface);
begin
  if not Assigned(aFile) then begin
    invalidSelection := True;
    Exit;
  end;

  if not Assigned(inspectedFile) then
    inspectedFile := aFile
  else if not Equals(inspectedFile, aFile) then
    invalidSelection := True;
end;


{
  Return the last record in the override chain whose containing file loads
  before the selected patch record.  This deliberately does not use
  WinningOverride, because the selected patch may itself be the winner.
}
function PreviousWinningOverride(aRecord: IInterface): IInterface;
var
  i: Integer;
  targetLoadOrder: Integer;
  masterRecord, candidate, candidateFile: IInterface;
begin
  Result := nil;
  if not Assigned(aRecord) then Exit;

  targetLoadOrder := GetLoadOrder(GetFile(aRecord));
  masterRecord := MasterOrSelf(aRecord);
  if not Assigned(masterRecord) then Exit;

  candidateFile := GetFile(masterRecord);
  if not Assigned(candidateFile) then Exit;

  if GetLoadOrder(candidateFile) >= targetLoadOrder then Exit;
  Result := masterRecord;

  if OverrideCount(masterRecord) = 0 then Exit;

  for i := 0 to Pred(OverrideCount(masterRecord)) do begin
    candidate := OverrideByIndex(masterRecord, i);
    if not Assigned(candidate) then Continue;

    candidateFile := GetFile(candidate);
    if not Assigned(candidateFile) then Continue;

    if GetLoadOrder(candidateFile) >= targetLoadOrder then
      Break;

    Result := candidate;
  end;
end;


procedure ReadXEZN(
  aRecord: IInterface;
  var isPresent: Boolean;
  var isResolved: Boolean;
  var resolvedFormID: Cardinal;
  var editValue: string);
var
  xezn, linkedRecord: IInterface;
begin
  isPresent := False;
  isResolved := False;
  resolvedFormID := 0;
  editValue := '';

  if not Assigned(aRecord) then Exit;

  xezn := ElementBySignature(aRecord, 'XEZN');
  if not Assigned(xezn) then Exit;

  editValue := Trim(GetEditValue(xezn));
  if editValue = '' then Exit;
  isPresent := True;

  linkedRecord := LinksTo(xezn);
  if Assigned(linkedRecord) then begin
    isResolved := True;
    resolvedFormID := GetLoadOrderFormID(linkedRecord);
  end;
end;


function XEZNValuesEqual(
  patchResolved: Boolean;
  patchFormID: Cardinal;
  const patchEditValue: string;
  previousResolved: Boolean;
  previousFormID: Cardinal;
  const previousEditValue: string): Boolean;
begin
  if patchResolved and previousResolved then
    Result := patchFormID = previousFormID
  else
    Result := SameText(patchEditValue, previousEditValue);
end;


function HasDisableCombatBoundary(aRecord: IInterface): Boolean;
var
  value: string;
begin
  Result := False;
  if not Assigned(aRecord) then Exit;

  value := GetElementEditValues(
    aRecord,
    'DATA - Data\Flags\Disable Combat Boundary');
  Result := SameText(value, '1');
end;


procedure AnalyzeXEZNRecord(
  aRecord: IInterface;
  var recordTotal: Integer;
  var presentCount: Integer;
  var addedCount: Integer;
  var changedExistingCount: Integer;
  var removedCount: Integer;
  var unchangedCount: Integer;
  var previousUnresolvedCount: Integer;
  var patchLinkUnresolvedCount: Integer;
  var previousLinkUnresolvedCount: Integer;
  var deletedCount: Integer);
var
  previous: IInterface;
  patchPresent, patchResolved: Boolean;
  previousPresent, previousResolved: Boolean;
  patchFormID, previousFormID: Cardinal;
  patchEditValue, previousEditValue: string;
begin
  Inc(recordTotal);

  if GetIsDeleted(aRecord) then begin
    Inc(deletedCount);
    Exit;
  end;

  ReadXEZN(
    aRecord,
    patchPresent,
    patchResolved,
    patchFormID,
    patchEditValue);

  if patchPresent then begin
    Inc(presentCount);
    if not patchResolved then
      Inc(patchLinkUnresolvedCount);
  end;

  previous := PreviousWinningOverride(aRecord);
  if not Assigned(previous) then begin
    Inc(previousUnresolvedCount);
    Exit;
  end;

  ReadXEZN(
    previous,
    previousPresent,
    previousResolved,
    previousFormID,
    previousEditValue);

  if previousPresent and not previousResolved then
    Inc(previousLinkUnresolvedCount);

  if patchPresent and not previousPresent then
    Inc(addedCount)
  else if not patchPresent and previousPresent then
    Inc(removedCount)
  else if not patchPresent and not previousPresent then
    Inc(unchangedCount)
  else if XEZNValuesEqual(
    patchResolved,
    patchFormID,
    patchEditValue,
    previousResolved,
    previousFormID,
    previousEditValue) then
    Inc(unchangedCount)
  else
    Inc(changedExistingCount);
end;


procedure AnalyzeECZN(aRecord: IInterface);
var
  previous: IInterface;
  patchHasBoundary, previousHasBoundary: Boolean;
begin
  Inc(ecznTotal);

  if GetIsDeleted(aRecord) then begin
    Inc(ecznDeleted);
    Exit;
  end;

  patchHasBoundary := HasDisableCombatBoundary(aRecord);
  if patchHasBoundary then
    Inc(ecznBoundaryPresent);

  previous := PreviousWinningOverride(aRecord);
  if not Assigned(previous) then begin
    Inc(ecznPreviousUnresolved);
    Exit;
  end;

  previousHasBoundary := HasDisableCombatBoundary(previous);

  if patchHasBoundary and not previousHasBoundary then
    Inc(ecznBoundaryAdded)
  else if patchHasBoundary and previousHasBoundary then
    Inc(ecznBoundaryAlreadyPresent)
  else if not patchHasBoundary and previousHasBoundary then
    Inc(ecznBoundaryRemoved)
  else
    Inc(ecznBoundaryUnchangedAbsent);
end;


procedure PrintXEZNSection(
  const sectionName: string;
  recordTotal: Integer;
  presentCount: Integer;
  addedCount: Integer;
  changedExistingCount: Integer;
  removedCount: Integer;
  unchangedCount: Integer;
  previousUnresolvedCount: Integer;
  patchLinkUnresolvedCount: Integer;
  previousLinkUnresolvedCount: Integer;
  deletedCount: Integer);
begin
  AddMessage(sectionName + ':');
  AddMessage('  Overrides in patch: ' + IntToStr(recordTotal));
  AddMessage('  XEZN present: ' + IntToStr(presentCount));
  AddMessage('  XEZN newly added: ' + IntToStr(addedCount));
  AddMessage(
    '  XEZN changed from an existing value: ' +
    IntToStr(changedExistingCount));
  AddMessage('  XEZN removed: ' + IntToStr(removedCount));
  AddMessage('  XEZN unchanged: ' + IntToStr(unchangedCount));
  AddMessage(
    '  Previous winner unresolved: ' +
    IntToStr(previousUnresolvedCount));
  AddMessage(
    '  Patch XEZN non-null but unresolved: ' +
    IntToStr(patchLinkUnresolvedCount));
  AddMessage(
    '  Previous XEZN non-null but unresolved: ' +
    IntToStr(previousLinkUnresolvedCount));
  AddMessage('  Deleted: ' + IntToStr(deletedCount));
  AddMessage('');
end;


function Initialize: Integer;
begin
  Result := 0;
  inspectedFile := nil;
  sawPluginSelection := False;
  invalidSelection := False;
  AddMessage('UAEZP Patch Inspector: collecting selected-plugin records...');
end;


function Process(e: IInterface): Integer;
var
  elementSignature: string;
begin
  Result := 0;

  if ElementType(e) = etFile then begin
    sawPluginSelection := True;
    ObserveFile(e);
    Exit;
  end;

  if ElementType(e) <> etMainRecord then Exit;

  ObserveFile(GetFile(e));
  elementSignature := Signature(e);

  if elementSignature = 'TES4' then begin
    sawPluginSelection := True;
    Exit;
  end;

  Inc(totalRecords);

  if elementSignature = 'CELL' then
    AnalyzeXEZNRecord(
      e,
      cellTotal,
      cellPresent,
      cellAdded,
      cellChangedExisting,
      cellRemoved,
      cellUnchanged,
      cellPreviousUnresolved,
      cellPatchLinkUnresolved,
      cellPreviousLinkUnresolved,
      cellDeleted)
  else if elementSignature = 'WRLD' then
    AnalyzeXEZNRecord(
      e,
      wrldTotal,
      wrldPresent,
      wrldAdded,
      wrldChangedExisting,
      wrldRemoved,
      wrldUnchanged,
      wrldPreviousUnresolved,
      wrldPatchLinkUnresolved,
      wrldPreviousLinkUnresolved,
      wrldDeleted)
  else if elementSignature = 'ECZN' then
    AnalyzeECZN(e);
end;


function Finalize: Integer;
var
  relevantCell, relevantWrld, relevantEczn: Integer;
  relevantTotal, forwardingTotal: Integer;
begin
  Result := 0;

  if not Assigned(inspectedFile) then begin
    AddMessage('ERROR: No plugin was selected. Select exactly one plugin and run Apply Script again.');
    Result := 1;
    Exit;
  end;

  if invalidSelection then begin
    AddMessage('ERROR: Records from more than one plugin were supplied. Select exactly one plugin.');
    Result := 1;
    Exit;
  end;

  if not sawPluginSelection then begin
    AddMessage('ERROR: The selection was not a file/plugin. Select the plugin node, not a group or record.');
    Result := 1;
    Exit;
  end;

  relevantCell := cellAdded + cellChangedExisting + cellRemoved;
  relevantWrld := wrldAdded + wrldChangedExisting + wrldRemoved;
  relevantEczn := ecznBoundaryAdded + ecznBoundaryRemoved;
  relevantTotal := relevantCell + relevantWrld + relevantEczn;
  forwardingTotal := cellUnchanged + wrldUnchanged +
    ecznBoundaryAlreadyPresent + ecznBoundaryUnchangedAbsent;

  AddMessage('============================================================');
  AddMessage(' UAEZP Patch Inspector');
  AddMessage('============================================================');
  AddMessage('');
  AddMessage('Plugin: ' + GetFileName(inspectedFile));
  AddMessage('Total records in patch: ' + IntToStr(totalRecords));
  AddMessage('');

  PrintXEZNSection(
    'CELL',
    cellTotal,
    cellPresent,
    cellAdded,
    cellChangedExisting,
    cellRemoved,
    cellUnchanged,
    cellPreviousUnresolved,
    cellPatchLinkUnresolved,
    cellPreviousLinkUnresolved,
    cellDeleted);

  PrintXEZNSection(
    'WRLD',
    wrldTotal,
    wrldPresent,
    wrldAdded,
    wrldChangedExisting,
    wrldRemoved,
    wrldUnchanged,
    wrldPreviousUnresolved,
    wrldPatchLinkUnresolved,
    wrldPreviousLinkUnresolved,
    wrldDeleted);

  AddMessage('ECZN:');
  AddMessage('  Overrides in patch: ' + IntToStr(ecznTotal));
  AddMessage(
    '  Disable Combat Boundary present: ' +
    IntToStr(ecznBoundaryPresent));
  AddMessage(
    '  Disable Combat Boundary newly added: ' +
    IntToStr(ecznBoundaryAdded));
  AddMessage(
    '  Already present before patch: ' +
    IntToStr(ecznBoundaryAlreadyPresent));
  AddMessage(
    '  Unchanged and absent: ' +
    IntToStr(ecznBoundaryUnchangedAbsent));
  AddMessage(
    '  Disable Combat Boundary removed: ' +
    IntToStr(ecznBoundaryRemoved));
  AddMessage(
    '  Previous winner unresolved: ' +
    IntToStr(ecznPreviousUnresolved));
  AddMessage('  Deleted: ' + IntToStr(ecznDeleted));
  AddMessage('');

  AddMessage('Directly comparable additions:');
  AddMessage('  CELL XEZN newly added: ' + IntToStr(cellAdded));
  AddMessage('  WRLD XEZN newly added: ' + IntToStr(wrldAdded));
  AddMessage(
    '  ECZN Disable Combat Boundary newly added: ' +
    IntToStr(ecznBoundaryAdded));
  AddMessage(
    '  Total: ' +
    IntToStr(cellAdded + wrldAdded + ecznBoundaryAdded));
  AddMessage('');

  AddMessage('Relevant UAEZP field/bit changes:');
  AddMessage('  CELL: ' + IntToStr(relevantCell));
  AddMessage('  WRLD: ' + IntToStr(relevantWrld));
  AddMessage('  ECZN: ' + IntToStr(relevantEczn));
  AddMessage('  Total: ' + IntToStr(relevantTotal));
  AddMessage('');

  AddMessage('No-relevant-change forwarding:');
  AddMessage('  CELL: ' + IntToStr(cellUnchanged));
  AddMessage('  WRLD: ' + IntToStr(wrldUnchanged));
  AddMessage(
    '  ECZN: ' +
    IntToStr(ecznBoundaryAlreadyPresent + ecznBoundaryUnchangedAbsent));
  AddMessage('  Total: ' + IntToStr(forwardingTotal));
  AddMessage('');
  AddMessage('Inspection complete. No records were modified.');
end;

end.
