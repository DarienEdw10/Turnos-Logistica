/**
 * Módulo de Programación de Producción (MPS)
 * Soporte para asignación individual y masiva (Multi-Celda y Rango de Fechas)
 */

let modoFechaActual = 'dia';
let celdasDisponibles = [];

// =============================================================
// 1. GESTIÓN MULTI-CELDA
// =============================================================
function toggleMenuCeldas() {
    if (celdasDisponibles.length > 0) {
        document.getElementById('menuCeldas').classList.toggle('show');
    }
}

// Cerrar el dropdown al hacer clic fuera
document.addEventListener('click', (e) => {
    const wrap = document.querySelector('.dropdown-multiselect');
    if (wrap && !wrap.contains(e.target)) {
        const menu = document.getElementById('menuCeldas');
        if (menu) menu.classList.remove('show');
    }
});

function toggleTodasCeldas() {
    const checks = document.querySelectorAll('.chk-celda');
    if (checks.length === 0) return;

    const todasMarcadas = Array.from(checks).every(c => c.checked);
    checks.forEach(c => c.checked = !todasMarcadas);
    actualizarTextoCeldas();
}

function actualizarTextoCeldas() {
    const seleccionadas = Array.from(document.querySelectorAll('.chk-celda:checked'));
    const lbl = document.getElementById('lblCeldasSeleccionadas');
    const hdnCelda = document.getElementById('hdnCeldaIdUnica');

    if (seleccionadas.length === 0) {
        lbl.textContent = '-- Seleccione Celdas --';
        hdnCelda.value = '';
        cargarPartes('');
    } else if (seleccionadas.length === 1) {
        lbl.textContent = seleccionadas[0].dataset.nombre;
        hdnCelda.value = seleccionadas[0].value;
        cargarPartes(seleccionadas[0].value);
    } else {
        lbl.textContent = `${seleccionadas.length} celdas seleccionadas`;
        hdnCelda.value = seleccionadas[0].value;
        cargarPartes(seleccionadas[0].value);
    }
}

// =============================================================
// 2. GESTIÓN DE MODOS DE FECHA
// =============================================================
function cambiarModoFecha(modo, boton) {
    modoFechaActual = modo;
    document.querySelectorAll('.date-pill').forEach(p => p.classList.remove('active'));
    boton.classList.add('active');

    document.getElementById('wrapModoDia').style.display = modo === 'dia' ? 'block' : 'none';
    document.getElementById('wrapModoSemana').style.display = modo === 'semana' ? 'block' : 'none';
    document.getElementById('wrapModoRango').style.display = modo === 'rango' ? 'flex' : 'none';

    const txtIndividual = document.getElementById('txtFechaIndividual');
    if (modo === 'dia') {
        txtIndividual.setAttribute('required', 'required');
    } else {
        txtIndividual.removeAttribute('required');
    }
}

function actualizarInfoSemana(valorSemana) {
    if (!valorSemana) return;
    const fechas = obtenerFechasDeSemana(valorSemana);
    const fInicio = fechas[0].toLocaleDateString('es-MX', { day: '2-digit', month: 'short' });
    const fFin = fechas[fechas.length - 1].toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' });
    document.getElementById('lblInfoSemana').textContent = `📅 Programando 6 turnos: Lun ${fInicio} al Sáb ${fFin}`;
}

function obtenerFechasDeSemana(weekStr) {
    const [year, week] = weekStr.split('-W').map(Number);
    const simple = new Date(year, 0, 1 + (week - 1) * 7);
    const dow = simple.getDay();
    const ISOweekStart = simple;
    if (dow <= 4)
        ISOweekStart.setDate(simple.getDate() - simple.getDay() + 1);
    else
        ISOweekStart.setDate(simple.getDate() + 8 - simple.getDay());

    const dias = [];
    for (let i = 0; i < 6; i++) {
        const d = new Date(ISOweekStart);
        d.setDate(d.getDate() + i);
        dias.push(d);
    }
    return dias;
}

function recopilarFechas() {
    const lista = [];
    if (modoFechaActual === 'dia') {
        const f = document.getElementById('txtFechaIndividual').value;
        if (f) lista.push(f);
    } 
    else if (modoFechaActual === 'semana') {
        const sem = document.getElementById('txtSemana').value;
        if (sem) {
            const fechas = obtenerFechasDeSemana(sem);
            fechas.forEach(d => lista.push(d.toISOString().split('T')[0]));
        }
    } 
    else if (modoFechaActual === 'rango') {
        const fInicioVal = document.getElementById('txtFechaDesde').value;
        const fFinVal = document.getElementById('txtFechaHasta').value;

        if (fInicioVal && fFinVal) {
            const fInicio = new Date(fInicioVal + 'T00:00:00');
            const fFin = new Date(fFinVal + 'T00:00:00');

            if (fInicio <= fFin) {
                let actual = new Date(fInicio);
                while (actual <= fFin) {
                    lista.push(actual.toISOString().split('T')[0]);
                    actual.setDate(actual.getDate() + 1);
                }
            }
        }
    }
    return lista;
}

// =============================================================
// 3. ENVÍO HÍBRIDO (INDIVIDUAL O MASIVO)
// =============================================================
async function interceptarEnvio(event) {
    const celdasSeleccionadas = Array.from(document.querySelectorAll('.chk-celda:checked')).map(c => parseInt(c.value));
    const fechas = recopilarFechas();

    if (celdasSeleccionadas.length === 0) {
        alert('Debe seleccionar al menos una celda / workcenter.');
        event.preventDefault();
        return false;
    }

    if (fechas.length === 0) {
        alert('Debe especificar una fecha o rango de producción válido.');
        event.preventDefault();
        return false;
    }

    // Si es 1 sola celda y 1 sola fecha, deja que el POST normal de MVC ocurra
    if (celdasSeleccionadas.length === 1 && fechas.length === 1 && modoFechaActual === 'dia') {
        return true; 
    }

    // Si son múltiples celdas o varias fechas, procesar vía AJAX
    event.preventDefault();

    const selTurno = document.querySelector('input[name="TurnoId"]:checked');
    const turnoId = selTurno ? parseInt(selTurno.value) : 0;
    const parteId = parseInt(document.getElementById('selParte').value || 0);
    const horas = parseFloat(document.getElementById('txtTiempoEstimado').value || 8);
    const razon = document.getElementById('txtRazonObligatoria').value.trim();

    if (!razon) {
        alert('La razón de la programación es obligatoria.');
        return false;
    }

    const btn = document.getElementById('btnSubmitProgramacion');
    btn.disabled = true;
    btn.innerText = 'Guardando programación masiva...';

    const payload = {
        celdaIds: celdasSeleccionadas,
        fechas: fechas,
        turnoId: turnoId,
        parteId: parteId,
        horasNetas: horas,
        jphPlaneado: 0,
        lotePlaneado: 0,
        razonCambio: razon
    };

    try {
        const resp = await fetch('/Programacion/GuardarProgramacionMasiva', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const res = await resp.json();

        if (res.success) {
            alert(`✔ ${res.message}`);
            window.location.href = '/Calendario';
        } else {
            alert(`✖ Error: ${res.message}`);
            btn.disabled = false;
            btn.innerText = 'Guardar Programación';
        }
    } catch (err) {
        alert(`✖ Error de red: ${err.message}`);
        btn.disabled = false;
        btn.innerText = 'Guardar Programación';
    }

    return false;
}

// =============================================================
// 4. CASCADA DE SELECTORES
// =============================================================
function actualizarHorasEstimadas(horas) {
    const inputHoras = document.getElementById('txtTiempoEstimado');
    if (inputHoras) {
        inputHoras.value = horas;
    }
}

function cargarLineas(proyectoId) {
    const selLinea = document.getElementById('selLinea');
    const menuCeldas = document.getElementById('menuCeldas');
    const lblCeldas = document.getElementById('lblCeldasSeleccionadas');
    const btnToggle = document.getElementById('btnToggleAllCeldas');
    const selParte = document.getElementById('selParte');

    selLinea.innerHTML = '<option value="">Cargando líneas...</option>';
    menuCeldas.innerHTML = '<div style="color:var(--text-muted); font-size:12px; padding:6px;">Seleccione una línea primero...</div>';
    lblCeldas.textContent = '-- Seleccione Línea Primero --';
    btnToggle.style.display = 'none';
    selParte.innerHTML = '<option value="">-- Seleccione Celda Primero --</option>';
    celdasDisponibles = [];

    if (!proyectoId) {
        selLinea.innerHTML = '<option value="">-- Seleccione Proyecto Primero --</option>';
        return;
    }

    fetch(`/Programacion/ObtenerLineasPorProyecto?proyectoId=${proyectoId}`)
        .then(res => res.json())
        .then(data => {
            selLinea.innerHTML = '<option value="">-- Seleccione Línea --</option>';
            data.forEach(item => {
                selLinea.innerHTML += `<option value="${item.id}">${item.texto}</option>`;
            });
        });
}

function cargarCeldas(lineaId) {
    const menuCeldas = document.getElementById('menuCeldas');
    const lblCeldas = document.getElementById('lblCeldasSeleccionadas');
    const btnToggle = document.getElementById('btnToggleAllCeldas');
    const selParte = document.getElementById('selParte');

    menuCeldas.innerHTML = '<div style="color:var(--text-muted); font-size:12px; padding:6px;">Cargando celdas...</div>';
    lblCeldas.textContent = '-- Seleccione Celdas --';
    selParte.innerHTML = '<option value="">-- Seleccione Celda Primero --</option>';
    celdasDisponibles = [];

    if (!lineaId) {
        menuCeldas.innerHTML = '<div style="color:var(--text-muted); font-size:12px; padding:6px;">Seleccione una línea primero...</div>';
        btnToggle.style.display = 'none';
        return;
    }

    fetch(`/Programacion/ObtenerCeldasPorLinea?lineaId=${lineaId}`)
        .then(res => res.json())
        .then(data => {
            celdasDisponibles = data;
            if (!data || data.length === 0) {
                menuCeldas.innerHTML = '<div style="color:var(--text-muted); font-size:12px; padding:6px;">No hay celdas para esta línea.</div>';
                btnToggle.style.display = 'none';
                return;
            }

            btnToggle.style.display = 'inline-block';
            menuCeldas.innerHTML = data.map(item => `
                <label class="multiselect-item">
                    <input type="checkbox" class="chk-celda" value="${item.id}" data-nombre="${item.texto}" onchange="actualizarTextoCeldas()" />
                    <span>${item.texto}</span>
                </label>
            `).join('');
        });
}

function cargarPartes(celdaId) {
    const selParte = document.getElementById('selParte');
    selParte.innerHTML = '<option value="">Cargando números de parte...</option>';

    if (!celdaId) {
        selParte.innerHTML = '<option value="">-- Seleccione Celda Primero --</option>';
        return;
    }

    fetch(`/Programacion/ObtenerPartesPorCelda?celdaId=${celdaId}`)
        .then(res => res.json())
        .then(data => {
            selParte.innerHTML = '<option value="">-- Seleccione Número de Parte --</option>';
            data.forEach(item => {
                selParte.innerHTML += `<option value="${item.id}">${item.texto}</option>`;
            });
        });
}
let diasSalteados = [];

function agregarDiaSalteado() {
    const input = document.getElementById('txtFechaSalteada');
    const valor = input.value;
    if (!valor) return;

    if (!diasSalteados.includes(valor)) {
        diasSalteados.push(valor);
        diasSalteados.sort();
        renderizarDiasSalteados();
    }
}

function removerDiaSalteado(fecha) {
    diasSalteados = diasSalteados.filter(f => f !== fecha);
    renderizarDiasSalteados();
}

function renderizarDiasSalteados() {
    const contenedor = document.getElementById('listaDiasSalteados');
    if (diasSalteados.length === 0) {
        contenedor.innerHTML = '<span style="font-size:11px; color:var(--text-muted); line-height:24px;">No has agregado días salteados aún.</span>';
        return;
    }

    contenedor.innerHTML = diasSalteados.map(f => {
        const d = new Date(f + 'T00:00:00');
        const formato = d.toLocaleDateString('es-MX', { weekday: 'short', day: '2-digit', month: 'short' });
        return `
            <span style="background:#2563eb; color:#fff; font-size:11px; font-weight:600; padding:2px 8px; border-radius:12px; display:inline-flex; align-items:center; gap:6px;">
                ${formato}
                <button type="button" onclick="removerDiaSalteado('${f}')" style="background:none; border:none; color:#fff; font-size:12px; cursor:pointer; padding:0; line-height:1;">&times;</button>
            </span>
        `;
    }).join('');
}