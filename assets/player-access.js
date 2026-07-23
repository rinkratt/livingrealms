document.querySelectorAll('[data-password-toggle]').forEach((button) => {
  button.addEventListener('click', () => {
    const field = document.getElementById(button.dataset.passwordToggle);
    if (!field) return;
    const showing = field.type === 'text';
    field.type = showing ? 'password' : 'text';
    button.textContent = showing ? 'Show' : 'Hide';
    button.setAttribute('aria-label', showing ? 'Show password' : 'Hide password');
    button.setAttribute('aria-pressed', showing ? 'false' : 'true');
    field.focus();
  });
});
