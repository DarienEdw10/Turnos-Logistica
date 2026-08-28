/**
 * Módulo de Gestión de Usuarios y Permisos
 * Sistema de Programación de Producción (MPS)
 */

document.addEventListener('DOMContentLoaded', () => {
    const txtBuscar = document.getElementById('txtBuscarEmpleado');
    const btnBuscar = document.getElementById('btnBuscarEmpleado');
    const tbody = document.getElementById('tblColaboradoresBody');
    const btnGuardar = document.getElementById('btnGuardarPermiso');
    const lblTotal = document.getElementById('lblTotalEmpleados');
    const msgBox = document.getElementById('msgAlerta');

    let debounceTimer;

    // 1. Cargar colaboradores desde la DLL corporativa
    async function cargarEmpleados(filtro = '') {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" style="text-align:center; padding:30px; color:var(--text-muted);">
                    Buscando en padrón corporativo...
                </td>
            </tr>`;

        try {
            const resp = await fetch(`/Usuarios/BuscarColaboradoresCorporativos?query=${encodeURIComponent(filtro)}`);
            const res = await resp.json();

            if (!res.success || !res.data || res.data.length === 0) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="5" style="text-align:center; padding:30px; color:var(--text-muted);">
                            No se encontraron colaboradores en el padrón.
                        </td>
                    </tr>`;
                lblTotal.textContent = '0 resultados';
                return;
            }

            lblTotal.textContent = `${res.data.length} mostrados`;

            tbody.innerHTML = res.data.map(e => `
                <tr class="row-empleado">
                    <td style="text-align:center; font-weight:700; font-family:monospace;">${e.numero || ''}</td>
                    <td><strong>${e.nombre || ''}</strong></td>
                    <td><span class="badge-cwid">${e.cwid || 'Sin CWID'}</span></td>
                    <td style="color:var(--text-muted); font-size:12px;">${e.planta || ''}</td>
                    <td style="text-align:center;">
                        <button type="button" class="btn btn-secondary btn-sm btn-seleccionar"
                                data-cwid="${e.cwid || ''}"
                                data-nombre="${e.nombre || ''}"
                                data-numero="${e.numero || ''}"
                                style="padding:2px 10px; font-size:12px;">
                            Elegir &rarr;
                        </button>
                    </td>
                </tr>
            `).join('');

            // 2. Asignar evento de selección a filas y botones
            document.querySelectorAll('.row-empleado').forEach(tr => {
                tr.addEventListener('click', () => {
                    const btn = tr.querySelector('.btn-seleccionar');
                    if (!btn) return;

                    const cwid = btn.dataset.cwid;
                    const nombre = btn.dataset.nombre;
                    const numero = btn.dataset.numero;

                    document.getElementById('txtColaboradorSeleccionado').value = `#${numero} - ${nombre} (${cwid || 'Sin CWID'})`;
                    document.getElementById('hdnCwid').value = cwid;
                    document.getElementById('hdnNombre').value = nombre;
                    document.getElementById('hdnNomina').value = numero;

                    btnGuardar.disabled = !cwid;
                    btnGuardar.style.opacity = !cwid ? '0.6' : '1';

                    document.querySelectorAll('.row-empleado').forEach(r => r.classList.remove('selected'));
                    tr.classList.add('selected');
                });
            });

        } catch (err) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="5" style="text-align:center; padding:30px; color:#dc2626;">
                        Error al consultar el padrón corporativo: ${err.message}
                    </td>
                </tr>`;
            lblTotal.textContent = 'Error';
        }
    }

    // 3. Búsqueda con debounce (300 ms)
    if (txtBuscar) {
        txtBuscar.addEventListener('input', () => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                cargarEmpleados(txtBuscar.value.trim());
            }, 300);
        });
    }

    if (btnBuscar) {
        btnBuscar.addEventListener('click', () => cargarEmpleados(txtBuscar.value.trim()));
    }

    // 4. Guardar Permiso en Base de Datos MPS
    if (btnGuardar) {
        btnGuardar.addEventListener('click', async () => {
            const cwid = document.getElementById('hdnCwid').value;
            const noEmpleado = document.getElementById('hdnNomina').value;
            const nombre = document.getElementById('hdnNombre').value;
            const nivel = parseInt(document.getElementById('ddlNivel').value, 10);

            if (!cwid) {
                alert('El colaborador seleccionado no tiene una cuenta de red (CWID) vinculada.');
                return;
            }

            btnGuardar.disabled = true;
            btnGuardar.innerText = 'Guardando...';

            try {
                const resp = await fetch('/Usuarios/AsignarNivel', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ cwid, noEmpleado, nombre, nivel })
                });
                const res = await resp.json();

                if (msgBox) {
                    msgBox.style.display = 'block';
                    if (res.success) {
                        msgBox.style.background = '#dcfce7';
                        msgBox.style.border = '1px solid #86efac';
                        msgBox.style.color = '#15803d';
                        msgBox.innerText = `✔ ${res.message}`;
                    } else {
                        msgBox.style.background = '#fee2e2';
                        msgBox.style.border = '1px solid #f87171';
                        msgBox.style.color = '#991b1b';
                        msgBox.innerText = `✖ ${res.message}`;
                    }
                }
            } catch (err) {
                if (msgBox) {
                    msgBox.style.display = 'block';
                    msgBox.style.background = '#fee2e2';
                    msgBox.style.border = '1px solid #f87171';
                    msgBox.style.color = '#991b1b';
                    msgBox.innerText = `✖ Error de comunicación: ${err.message}`;
                }
            } finally {
                btnGuardar.disabled = false;
                btnGuardar.innerHTML = `
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>
                    </svg> Guardar Permiso`;
            }
        });
    }

    // Carga inicial
    cargarEmpleados();
});