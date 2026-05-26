import { Edit, Image, Plus, Trash2 } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { api, getErrorMessage, routes } from '../api/client.js';
import { EmptyState, ErrorBanner, Field, LoadingState, Modal, PageHeader, SearchInput } from '../components/ui.jsx';
import { normalizeText } from '../utils/format.js';

const blankMedication = {
  tradeName: '',
  description: '',
  dosageForm: '',
  imageUrl: ''
};

const blankIngredient = {
  ingredientName: '',
  strengthValue: '',
  strengthUnit: ''
};

export function MedicationsPage() {
  const [medications, setMedications] = useState([]);
  const [ingredients, setIngredients] = useState([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [modal, setModal] = useState(null);
  const [form, setForm] = useState(blankMedication);
  const [ingredientForm, setIngredientForm] = useState(blankIngredient);
  const [selectedMedication, setSelectedMedication] = useState(null);

  async function loadData() {
    setLoading(true);
    setError('');
    try {
      const [medicationResponse, ingredientResponse] = await Promise.all([
        api.get(routes.medications),
        api.get(routes.ingredients).catch(() => ({ data: [] }))
      ]);
      setMedications(medicationResponse.data || []);
      setIngredients(ingredientResponse.data || []);
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to load medications.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  const filteredMedications = useMemo(() => {
    const needle = normalizeText(query);
    if (!needle) return medications;
    return medications.filter((medication) =>
      [medication.trade_name, medication.Trade_name, medication.description, medication.Description, medication.dosage_Form, medication.Dosage_Form]
        .some((value) => normalizeText(value).includes(needle))
    );
  }, [medications, query]);

  function medicationName(medication) {
    return medication.trade_name || medication.Trade_name || medication.tradeName || medication.MedicationName || '';
  }

  function medicationId(medication) {
    return medication.id || medication.ID;
  }

  function medicationImage(medication) {
    return medication.image_url || medication.ImageUrl || medication.imageUrl;
  }

  function medicationDescription(medication) {
    return medication.description || medication.Description || '';
  }

  function dosageForm(medication) {
    return medication.dosage_Form || medication.Dosage_Form || medication.dosageForm || '';
  }

  function openCreate() {
    setForm(blankMedication);
    setModal({ type: 'create', title: 'Add medication' });
  }

  function openEdit(medication) {
    setForm({
      id: medicationId(medication),
      tradeName: medicationName(medication),
      description: medicationDescription(medication),
      dosageForm: dosageForm(medication),
      imageUrl: medicationImage(medication) || ''
    });
    setModal({ type: 'edit', title: `Edit ${medicationName(medication)}` });
  }

  async function saveMedication(event) {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      const payload = {
        tradeName: form.tradeName,
        description: form.description || null,
        dosageForm: form.dosageForm || null,
        imageUrl: form.imageUrl || null
      };

      if (modal.type === 'edit') {
        await api.put(routes.updateMedication(form.id), payload);
      } else {
        await api.post(routes.addMedication, payload);
      }

      setModal(null);
      await loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to save medication.'));
    } finally {
      setSaving(false);
    }
  }

  async function deleteMedication(medication) {
    const name = medicationName(medication);
    const confirmed = window.confirm(`Delete ${name}? This can affect users linked to the medication.`);
    if (!confirmed) return;
    setError('');
    try {
      await api.delete(routes.deleteMedication(medicationId(medication)));
      await loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to delete medication.'));
    }
  }

  async function addIngredient(event) {
    event.preventDefault();
    if (!selectedMedication) return;
    setSaving(true);
    setError('');
    try {
      await api.post(routes.addIngredients, {
        medicationName: medicationName(selectedMedication),
        ingredients: [
          {
            ingredientName: ingredientForm.ingredientName,
            strengthValue: ingredientForm.strengthValue,
            strengthUnit: ingredientForm.strengthUnit
          }
        ]
      });
      setIngredientForm(blankIngredient);
      await loadData();
    } catch (err) {
      setError(getErrorMessage(err, 'Unable to add ingredient.'));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <LoadingState label="Loading medications..." />;

  return (
    <div className="stack">
      <PageHeader
        title="Medication catalog"
        description="Manage catalog entries and image URLs. Ingredient linking uses the existing medication ingredient API."
        actions={
          <button className="primary-button" type="button" onClick={openCreate}>
            <Plus size={17} />
            Add medication
          </button>
        }
      />
      <ErrorBanner message={error} />

      <div className="toolbar">
        <SearchInput value={query} onChange={setQuery} placeholder="Search medications by name, description, or form" />
      </div>

      <div className="catalog-grid">
        {filteredMedications.length === 0 ? (
          <EmptyState title="No medications found" description="Add a medication or adjust your search." />
        ) : (
          filteredMedications.map((medication) => {
            const imageUrl = medicationImage(medication);
            return (
              <article className="med-card" key={medicationId(medication)}>
                <div className="med-image">
                  {imageUrl ? <img src={imageUrl} alt={medicationName(medication)} /> : <Image size={24} />}
                </div>
                <div className="med-body">
                  <div>
                    <h3>{medicationName(medication)}</h3>
                    <p>{medicationDescription(medication) || 'No description provided.'}</p>
                  </div>
                  <div className="med-meta">
                    <span>{dosageForm(medication) || 'No dosage form'}</span>
                    <span>{medication.ingredients?.length || medication.Ingredients?.length || 0} ingredients</span>
                  </div>
                  <div className="card-actions">
                    <button className="ghost-button compact" type="button" onClick={() => setSelectedMedication(medication)}>
                      Ingredients
                    </button>
                    <button className="icon-button" type="button" onClick={() => openEdit(medication)} aria-label="Edit medication" title="Edit medication">
                      <Edit size={17} />
                    </button>
                    <button className="icon-button danger" type="button" onClick={() => deleteMedication(medication)} aria-label="Delete medication" title="Delete medication">
                      <Trash2 size={17} />
                    </button>
                  </div>
                </div>
              </article>
            );
          })
        )}
      </div>

      {selectedMedication && (
        <Modal title={`Ingredients for ${medicationName(selectedMedication)}`} onClose={() => setSelectedMedication(null)} width="wide">
          <div className="two-column">
            <div>
              <h4>Current ingredients</h4>
              <div className="ingredient-list">
                {(selectedMedication.ingredients || selectedMedication.Ingredients || []).length === 0 ? (
                  <EmptyState title="No ingredients linked" />
                ) : (
                  (selectedMedication.ingredients || selectedMedication.Ingredients || []).map((item, index) => (
                    <div className="ingredient-row" key={`${item.ingredientName || item.IngredientName}-${index}`}>
                      <strong>{item.ingredientName || item.IngredientName}</strong>
                      <span>{item.strength_value || item.Strength_value || item.strengthValue || ''} {item.strength_unit || item.Strength_unit || item.strengthUnit || ''}</span>
                    </div>
                  ))
                )}
              </div>
            </div>
            <form className="stack" onSubmit={addIngredient}>
              <h4>Add ingredient link</h4>
              <Field label="Ingredient">
                <input
                  list="ingredient-options"
                  value={ingredientForm.ingredientName}
                  onChange={(event) => setIngredientForm({ ...ingredientForm, ingredientName: event.target.value })}
                  required
                />
              </Field>
              <datalist id="ingredient-options">
                {ingredients.map((ingredient) => (
                  <option key={ingredient.id || ingredient.Id} value={ingredient.ingredientName || ingredient.IngredientName} />
                ))}
              </datalist>
              <Field label="Strength value">
                <input value={ingredientForm.strengthValue} onChange={(event) => setIngredientForm({ ...ingredientForm, strengthValue: event.target.value })} />
              </Field>
              <Field label="Strength unit">
                <input value={ingredientForm.strengthUnit} onChange={(event) => setIngredientForm({ ...ingredientForm, strengthUnit: event.target.value })} />
              </Field>
              <button className="primary-button" type="submit" disabled={saving}>{saving ? 'Adding...' : 'Add ingredient'}</button>
            </form>
          </div>
        </Modal>
      )}

      {modal && (
        <Modal title={modal.title} onClose={() => setModal(null)}>
          <form className="form-grid single" onSubmit={saveMedication}>
            <Field label="Trade name">
              <input value={form.tradeName} onChange={(event) => setForm({ ...form, tradeName: event.target.value })} required />
            </Field>
            <Field label="Description">
              <textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} rows={4} />
            </Field>
            <Field label="Dosage form">
              <input value={form.dosageForm} onChange={(event) => setForm({ ...form, dosageForm: event.target.value })} />
            </Field>
            <Field label="Medication image URL">
              <input type="url" value={form.imageUrl} onChange={(event) => setForm({ ...form, imageUrl: event.target.value })} placeholder="https://..." />
            </Field>
            <div className="form-actions">
              <button className="ghost-button" type="button" onClick={() => setModal(null)}>Cancel</button>
              <button className="primary-button" type="submit" disabled={saving}>
                {saving ? 'Saving...' : 'Save medication'}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
